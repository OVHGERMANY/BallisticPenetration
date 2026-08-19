#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using BallisticPenetration.Core.Physics;
using BallisticPenetration.Runtime.Diagnostics;
using BallisticPenetration.Runtime.State;
using EFT.Ballistics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace BallisticPenetration.Runtime.Rendering
{
    /// <summary>
    /// Main-thread visual pool for physical components. Collision hooks enqueue immutable ownership
    /// commands only; all Unity object creation, mutation, culling, and destruction happens from
    /// Plugin.Update. Live entries retain the exact pool-safe Shot binding that created them.
    /// </summary>
    internal static class PhysicalProjectileVisualRuntime
    {
        private const int MaximumPendingCommands = 8192;
        private const string HostName = "BallisticPenetration.PhysicalGeometry";

        private static readonly PhysicalVisualCommandBuffer<VisualCommand> PendingCommands =
            new PhysicalVisualCommandBuffer<VisualCommand>(MaximumPendingCommands);
        private static readonly List<VisualCommand> CommandBatch = new List<VisualCommand>();
        private static readonly Dictionary<long, ActiveVisual> ActiveByToken =
            new Dictionary<long, ActiveVisual>();
        private static readonly Dictionary<PhysicalShotBinding, long> TokenByBinding =
            new Dictionary<PhysicalShotBinding, long>(PhysicalShotBindingReferenceComparer.Instance);
        private static readonly LinkedList<long> RegistrationOrder =
            new LinkedList<long>();
        private static readonly List<ActiveVisual> CandidateBuffer =
            new List<ActiveVisual>();
        private static readonly List<long> RetirementBuffer = new List<long>();
        private static readonly List<VisualSlot> Slots = new List<VisualSlot>();
        private static readonly Dictionary<PhysicalProjectileShapeClass, Mesh> Meshes =
            new Dictionary<PhysicalProjectileShapeClass, Mesh>();
        private static readonly Dictionary<PhysicalVisualMaterialKey, Material> Materials =
            new Dictionary<PhysicalVisualMaterialKey, Material>();
        private static readonly PhysicalVisualOwnershipLedger Ownership =
            new PhysicalVisualOwnershipLedger(PhysicalVisualPolicy.MaximumVisibleCapacity);

        private static long _nextOwnerToken;
        private static GameObject? _host;
        private static Camera? _camera;
        private static int _sceneHandle = int.MinValue;
        private static bool _assetsUnavailable;
        private static bool _failureLogged;

        internal static void RegisterLive(Shot shot, PhysicalShotBinding binding)
        {
            if (shot == null || binding == null || !binding.Matches(shot))
            {
                return;
            }

            long token = NextOwnerToken();
            if (token <= 0)
            {
                return;
            }

            Enqueue(new VisualCommand(
                VisualCommandKind.RegisterLive,
                token,
                shot,
                binding,
                binding.State));
        }

        internal static void RegisterEmbedded(PhysicalProjectileState state)
        {
            if (state == null)
            {
                return;
            }

            long token = NextOwnerToken();
            if (token <= 0)
            {
                return;
            }

            Enqueue(new VisualCommand(
                VisualCommandKind.RegisterEmbedded,
                token,
                null,
                null,
                state));
        }

        internal static void Retire(PhysicalShotBinding binding)
        {
            if (binding != null)
            {
                Enqueue(new VisualCommand(
                    VisualCommandKind.RetireBinding,
                    0L,
                    null,
                    binding,
                    null));
            }
        }

        internal static void UpdatePresentation()
        {
            PluginConfiguration? configuration = Plugin.Configuration;
            PhysicalVisualPolicy? policy = null;
            if (configuration == null
                || !configuration.Enabled.Value
                || !configuration.EnableExperimentalPhysicalProjectiles.Value
                || !configuration.RenderPhysicalComponents.Value
                || !configuration.TryGetVisualPolicy(out policy)
                || policy == null)
            {
                ClearQueuedCommands();
                ClearActiveVisuals();
                return;
            }

            try
            {
                int currentSceneHandle = SceneManager.GetActiveScene().handle;
                if (_sceneHandle != currentSceneHandle)
                {
                    ClearQueuedCommands();
                    ClearActiveVisuals();
                    _camera = null;
                    _sceneHandle = currentSceneHandle;
                }

                DrainCommands(policy);
                TrimTrackedCapacity(policy.MaximumTrackedComponents);
                EnforceVisibleCapacity(policy.MaximumVisibleComponents);
                if (ActiveByToken.Count == 0)
                {
                    DisableAllSlots();
                    return;
                }

                if (!EnsureAssets())
                {
                    ClearActiveVisuals();
                    return;
                }

                Camera? camera = ResolveCamera();
                if (camera == null)
                {
                    DisableAllSlots();
                    return;
                }

                UpdateCandidates(policy, camera.transform.position);
                AssignVisibleSlots(policy);
            }
            catch (UnityException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (MissingReferenceException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (MissingComponentException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (ArgumentException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (InvalidOperationException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (KeyNotFoundException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (NullReferenceException exception)
            {
                HandlePresentationFailure(exception);
            }
            catch (OverflowException exception)
            {
                HandlePresentationFailure(exception);
            }
        }

        internal static void Shutdown()
        {
            ClearQueuedCommands();
            ClearActiveVisuals();
            for (int index = 0; index < Slots.Count; index++)
            {
                VisualSlot slot = Slots[index];
                if (slot.GameObject != null)
                {
                    UnityObject.Destroy(slot.GameObject);
                }
            }

            Slots.Clear();
            foreach (Mesh mesh in Meshes.Values)
            {
                if (mesh != null)
                {
                    UnityObject.Destroy(mesh);
                }
            }

            Meshes.Clear();
            foreach (Material material in Materials.Values)
            {
                if (material != null)
                {
                    UnityObject.Destroy(material);
                }
            }

            Materials.Clear();
            if (_host != null)
            {
                UnityObject.Destroy(_host);
            }

            _host = null;
            _camera = null;
            _sceneHandle = int.MinValue;
            _assetsUnavailable = false;
            _failureLogged = false;
            Ownership.Reset();
        }

        private static void HandlePresentationFailure(Exception exception)
        {
            LogFailureOnce("Physical component renderer", exception);
            ClearQueuedCommands();
            ClearActiveVisuals();
        }

        private static void DrainCommands(PhysicalVisualPolicy policy)
        {
            int commandCount = PendingCommands.DrainTo(
                CommandBatch,
                policy.MaximumCommandsProcessedPerFrame);
            try
            {
                for (int index = 0; index < commandCount; index++)
                {
                    VisualCommand command = CommandBatch[index];
                    switch (command.Kind)
                    {
                        case VisualCommandKind.RegisterLive:
                            ProcessLiveRegistration(command, policy);
                            break;
                        case VisualCommandKind.RegisterEmbedded:
                            ProcessEmbeddedRegistration(command, policy);
                            break;
                        case VisualCommandKind.RetireBinding:
                            if (command.Binding != null
                                && TokenByBinding.TryGetValue(command.Binding, out long token))
                            {
                                RemoveActive(token, "binding-retired");
                            }

                            break;
                    }
                }
            }
            finally
            {
                CommandBatch.Clear();
            }
        }

        private static void ProcessLiveRegistration(
            VisualCommand command,
            PhysicalVisualPolicy policy)
        {
            if (command.Shot == null
                || command.Binding == null
                || command.State == null
                || !command.Binding.Matches(command.Shot)
                || !PhysicalShotBindingStore.TryGet(
                    command.Shot,
                    out PhysicalShotBinding? currentBinding)
                || !ReferenceEquals(currentBinding, command.Binding)
                || !TryCreatePose(command.State, policy, out PhysicalVisualPose pose))
            {
                return;
            }

            if (TokenByBinding.TryGetValue(command.Binding, out long existingToken))
            {
                RemoveActive(existingToken, "binding-reregistered");
            }

            EnsureTrackedCapacity(policy.MaximumTrackedComponents);
            var visual = new ActiveVisual(
                command.OwnerToken,
                command.Shot,
                command.Binding,
                command.State,
                pose,
                false,
                double.PositiveInfinity);
            AddActive(visual);
        }

        private static void ProcessEmbeddedRegistration(
            VisualCommand command,
            PhysicalVisualPolicy policy)
        {
            if (command.State == null
                || !TryCreatePose(command.State, policy, out PhysicalVisualPose pose))
            {
                return;
            }

            EnsureTrackedCapacity(policy.MaximumTrackedComponents);
            double expiresAt = Time.realtimeSinceStartupAsDouble + policy.EmbeddedLifetimeSeconds;
            var visual = new ActiveVisual(
                command.OwnerToken,
                null,
                null,
                command.State,
                pose,
                true,
                expiresAt);
            AddActive(visual);
        }

        private static void UpdateCandidates(
            PhysicalVisualPolicy policy,
            Vector3 cameraPosition)
        {
            CandidateBuffer.Clear();
            RetirementBuffer.Clear();
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (KeyValuePair<long, ActiveVisual> pair in ActiveByToken)
            {
                ActiveVisual visual = pair.Value;
                if (!visual.TryUpdatePosition(now, out Vector3 position))
                {
                    PhysicalProjectileLifecycleDiagnostics.Record(
                        "visual-retired",
                        visual.Shot,
                        visual.Binding,
                        visual.RetirementReason);
                    RetirementBuffer.Add(pair.Key);
                    continue;
                }

                Vector3 delta = position - cameraPosition;
                double distanceSquared = delta.sqrMagnitude;
                visual.DistanceSquaredMetres = distanceSquared;
                if (policy.IsWithinCullingDistance(distanceSquared))
                {
                    CandidateBuffer.Add(visual);
                }
                else
                {
                    ReleaseSlot(visual);
                }
            }

            for (int index = 0; index < RetirementBuffer.Count; index++)
            {
                RemoveActive(RetirementBuffer[index], "visual-invalid", false);
            }

            CandidateBuffer.Sort(CompareCandidates);
        }

        private static void AssignVisibleSlots(PhysicalVisualPolicy policy)
        {
            int visibleCount = Math.Min(
                policy.MaximumVisibleComponents,
                CandidateBuffer.Count);
            for (int index = visibleCount; index < CandidateBuffer.Count; index++)
            {
                ReleaseSlot(CandidateBuffer[index]);
            }

            for (int index = 0; index < visibleCount; index++)
            {
                ActiveVisual visual = CandidateBuffer[index];
                if (visual.HasLease
                    && visual.Lease.Slot >= policy.MaximumVisibleComponents)
                {
                    ReleaseSlot(visual);
                }

                if (!visual.HasLease)
                {
                    if (!Ownership.TryAcquire(
                            visual.OwnerToken,
                            policy.MaximumVisibleComponents,
                            out PhysicalVisualLease lease))
                    {
                        continue;
                    }

                    visual.SetLease(lease);
                }

                if (!Ownership.IsCurrent(visual.Lease))
                {
                    visual.ClearLease();
                    continue;
                }

                if (!EnsureSlot(visual.Lease.Slot))
                {
                    ReleaseSlot(visual);
                    continue;
                }

                ApplyVisual(Slots[visual.Lease.Slot], visual);
            }
        }

        private static void ApplyVisual(VisualSlot slot, ActiveVisual visual)
        {
            if (!Meshes.TryGetValue(visual.Pose.ShapeClass, out Mesh mesh)
                || !Materials.TryGetValue(visual.Pose.MaterialKey, out Material material))
            {
                ReleaseSlot(visual);
                return;
            }

            slot.MeshFilter.sharedMesh = mesh;
            slot.MeshRenderer.sharedMaterial = material;
            Transform transform = slot.GameObject.transform;
            transform.position = visual.CurrentPosition;
            PhysicalOrientation orientation = visual.CurrentOrientation;
            transform.rotation = new Quaternion(
                (float)orientation.X,
                (float)orientation.Y,
                (float)orientation.Z,
                (float)orientation.W);
            PhysicalVector3 scale = visual.Pose.ScaleMetres;
            transform.localScale = new Vector3(
                (float)scale.X,
                (float)scale.Y,
                (float)scale.Z);
            if (!slot.GameObject.activeSelf)
            {
                slot.GameObject.SetActive(true);
            }
        }

        private static bool EnsureAssets()
        {
            if (_assetsUnavailable)
            {
                return false;
            }

            if (_host == null)
            {
                _host = new GameObject(HostName)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                UnityObject.DontDestroyOnLoad(_host);
            }

            if (Meshes.Count == 0 && !CreateMeshes())
            {
                _assetsUnavailable = true;
                return false;
            }

            if (Materials.Count == 0 && !CreateMaterials())
            {
                _assetsUnavailable = true;
                return false;
            }

            return true;
        }

        private static bool CreateMeshes()
        {
            for (PhysicalProjectileShapeClass shape = PhysicalProjectileShapeClass.Spitzer;
                 shape <= PhysicalProjectileShapeClass.Flechette;
                 shape++)
            {
                if (!PhysicalProjectileVisualGeometry.TryCreateUnitMesh(
                        shape,
                        out PhysicalVisualMeshDescriptor? descriptor,
                        out _)
                    || descriptor == null)
                {
                    return false;
                }

                var vertices = new Vector3[descriptor.Vertices.Count];
                var triangles = new int[descriptor.Triangles.Count];
                for (int index = 0; index < vertices.Length; index++)
                {
                    PhysicalVector3 vertex = descriptor.Vertices[index];
                    vertices[index] = new Vector3(
                        (float)vertex.X,
                        (float)vertex.Y,
                        (float)vertex.Z);
                }

                for (int index = 0; index < triangles.Length; index++)
                {
                    triangles[index] = descriptor.Triangles[index];
                }

                var mesh = new Mesh
                {
                    name = "BallisticPenetration." + shape,
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = vertices,
                    triangles = triangles
                };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                Meshes.Add(shape, mesh);
            }

            return true;
        }

        private static bool CreateMaterials()
        {
            Shader? shader = Shader.Find("Standard")
                ?? Shader.Find("Legacy Shaders/Diffuse")
                ?? Shader.Find("Unlit/Color");
            if (shader == null || !shader.isSupported)
            {
                return false;
            }

            AddMaterial(PhysicalVisualMaterialKey.Unknown, shader, new Color(0.38f, 0.38f, 0.38f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.LeadAndCopper, shader, new Color(0.50f, 0.27f, 0.12f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.SteelCore, shader, new Color(0.45f, 0.39f, 0.28f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TungstenCore, shader, new Color(0.26f, 0.27f, 0.29f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Copper, shader, new Color(0.62f, 0.30f, 0.12f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Steel, shader, new Color(0.44f, 0.47f, 0.50f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Frangible, shader, new Color(0.30f, 0.32f, 0.34f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TargetMetal, shader, new Color(0.46f, 0.49f, 0.52f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TargetCeramic, shader, new Color(0.72f, 0.70f, 0.64f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TargetMineral, shader, new Color(0.40f, 0.36f, 0.31f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TargetOrganic, shader, new Color(0.36f, 0.22f, 0.14f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.TargetOther, shader, new Color(0.35f, 0.37f, 0.35f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Aluminum, shader, new Color(0.62f, 0.65f, 0.68f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Brass, shader, new Color(0.62f, 0.48f, 0.16f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Zinc, shader, new Color(0.50f, 0.54f, 0.57f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.NonMetallic, shader, new Color(0.20f, 0.28f, 0.36f, 1f));
            AddMaterial(PhysicalVisualMaterialKey.Lead, shader, new Color(0.30f, 0.31f, 0.34f, 1f));
            return true;
        }

        private static void AddMaterial(
            PhysicalVisualMaterialKey key,
            Shader shader,
            Color color)
        {
            var material = new Material(shader)
            {
                name = "BallisticPenetration." + key,
                color = color,
                enableInstancing = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            Materials.Add(key, material);
        }

        private static bool EnsureSlot(int slotIndex)
        {
            if (_host == null || slotIndex < 0 || slotIndex >= Ownership.Capacity)
            {
                return false;
            }

            while (Slots.Count <= slotIndex)
            {
                Slots.Add(CreateVisualSlot(Slots.Count));
            }

            if (!Slots[slotIndex].IsValid)
            {
                VisualSlot invalidSlot = Slots[slotIndex];
                if (invalidSlot.GameObject != null)
                {
                    UnityObject.Destroy(invalidSlot.GameObject);
                }

                Slots[slotIndex] = CreateVisualSlot(slotIndex);
            }

            return true;
        }

        private static VisualSlot CreateVisualSlot(int index)
        {
            var gameObject = new GameObject(
                "BallisticPenetration.PhysicalComponent." + index)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            gameObject.transform.SetParent(_host!.transform, false);
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = true;
            gameObject.SetActive(false);
            return new VisualSlot(gameObject, meshFilter, meshRenderer);
        }

        private static Camera? ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            return _camera != null && _camera.isActiveAndEnabled ? _camera : null;
        }

        private static bool TryCreatePose(
            PhysicalProjectileState state,
            PhysicalVisualPolicy policy,
            out PhysicalVisualPose pose)
        {
            return PhysicalProjectileVisualGeometry.TryCreatePose(
                state,
                policy.DimensionScale,
                policy.MinimumRenderedDiameterMetres,
                out pose,
                out _);
        }

        private static void AddActive(ActiveVisual visual)
        {
            ActiveByToken.Add(visual.OwnerToken, visual);
            visual.OrderNode = RegistrationOrder.AddLast(visual.OwnerToken);
            if (visual.Binding != null)
            {
                TokenByBinding[visual.Binding] = visual.OwnerToken;
            }
        }

        private static void EnsureTrackedCapacity(int maximumTrackedComponents)
        {
            while (ActiveByToken.Count >= maximumTrackedComponents
                && RegistrationOrder.First != null)
            {
                RemoveActive(RegistrationOrder.First.Value, "tracked-capacity");
            }
        }

        private static void TrimTrackedCapacity(int maximumTrackedComponents)
        {
            while (ActiveByToken.Count > maximumTrackedComponents
                && RegistrationOrder.First != null)
            {
                RemoveActive(RegistrationOrder.First.Value, "tracked-capacity");
            }
        }

        private static void EnforceVisibleCapacity(int maximumVisibleComponents)
        {
            foreach (KeyValuePair<long, ActiveVisual> pair in ActiveByToken)
            {
                ActiveVisual visual = pair.Value;
                if (visual.HasLease && visual.Lease.Slot >= maximumVisibleComponents)
                {
                    ReleaseSlot(visual);
                }
            }

            TrimSlotPool(maximumVisibleComponents);
        }

        private static void RemoveActive(
            long token,
            string reason = "renderer-clear",
            bool recordLifecycle = true)
        {
            if (!ActiveByToken.TryGetValue(token, out ActiveVisual visual))
            {
                return;
            }

            if (recordLifecycle)
            {
                PhysicalProjectileLifecycleDiagnostics.Record(
                    "visual-retired",
                    visual.Shot,
                    visual.Binding,
                    reason);
            }

            ReleaseSlot(visual);
            if (visual.Binding != null
                && TokenByBinding.TryGetValue(visual.Binding, out long currentToken)
                && currentToken == token)
            {
                TokenByBinding.Remove(visual.Binding);
            }

            if (visual.OrderNode != null)
            {
                RegistrationOrder.Remove(visual.OrderNode);
            }

            ActiveByToken.Remove(token);
        }

        private static void ReleaseSlot(ActiveVisual visual)
        {
            if (!visual.HasLease)
            {
                return;
            }

            PhysicalVisualLease lease = visual.Lease;
            if (Ownership.Release(lease)
                && lease.Slot >= 0
                && lease.Slot < Slots.Count)
            {
                VisualSlot slot = Slots[lease.Slot];
                if (slot.IsValid)
                {
                    slot.GameObject.SetActive(false);
                }
            }

            visual.ClearLease();
        }

        private static void ClearActiveVisuals()
        {
            if (ActiveByToken.Count > 0)
            {
                long[] tokens = new long[ActiveByToken.Count];
                ActiveByToken.Keys.CopyTo(tokens, 0);
                for (int index = 0; index < tokens.Length; index++)
                {
                    RemoveActive(tokens[index]);
                }
            }

            ActiveByToken.Clear();
            TokenByBinding.Clear();
            RegistrationOrder.Clear();
            CandidateBuffer.Clear();
            RetirementBuffer.Clear();
            Ownership.Reset();
            DisableAllSlots();
        }

        private static void DisableAllSlots()
        {
            for (int index = 0; index < Slots.Count; index++)
            {
                VisualSlot slot = Slots[index];
                if (slot.IsValid && slot.GameObject.activeSelf)
                {
                    slot.GameObject.SetActive(false);
                }
            }
        }

        private static void TrimSlotPool(int maximumVisibleComponents)
        {
            while (Slots.Count > maximumVisibleComponents)
            {
                int lastIndex = Slots.Count - 1;
                VisualSlot slot = Slots[lastIndex];
                if (slot.GameObject != null)
                {
                    UnityObject.Destroy(slot.GameObject);
                }

                Slots.RemoveAt(lastIndex);
            }
        }

        private static void ClearQueuedCommands()
        {
            PendingCommands.Clear();
        }

        private static void Enqueue(VisualCommand command)
        {
            _ = PendingCommands.Enqueue(command);
        }

        private static long NextOwnerToken()
        {
            long token = Interlocked.Increment(ref _nextOwnerToken);
            return token > 0 ? token : 0L;
        }

        private static int CompareCandidates(ActiveVisual left, ActiveVisual right)
        {
            int distanceComparison = left.DistanceSquaredMetres.CompareTo(
                right.DistanceSquaredMetres);
            return distanceComparison != 0
                ? distanceComparison
                : left.OwnerToken.CompareTo(right.OwnerToken);
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "A renderer diagnostic must not replace a game or plugin exception.")]
        private static void LogFailureOnce(string operation, Exception exception)
        {
            if (_failureLogged)
            {
                return;
            }

            _failureLogged = true;
            try
            {
                Plugin.Log?.LogWarning(
                    operation + " failed and was disabled for this session. " + exception);
            }
            catch
            {
                // Rendering is optional and must fail open.
            }
        }

        private enum VisualCommandKind
        {
            RegisterLive = 0,
            RegisterEmbedded = 1,
            RetireBinding = 2
        }

        private readonly struct VisualCommand
        {
            internal VisualCommand(
                VisualCommandKind kind,
                long ownerToken,
                Shot? shot,
                PhysicalShotBinding? binding,
                PhysicalProjectileState? state)
            {
                Kind = kind;
                OwnerToken = ownerToken;
                Shot = shot;
                Binding = binding;
                State = state;
            }

            internal VisualCommandKind Kind { get; }

            internal long OwnerToken { get; }

            internal Shot? Shot { get; }

            internal PhysicalShotBinding? Binding { get; }

            internal PhysicalProjectileState? State { get; }
        }

        private sealed class PhysicalShotBindingReferenceComparer :
            IEqualityComparer<PhysicalShotBinding>
        {
            internal static readonly PhysicalShotBindingReferenceComparer Instance =
                new PhysicalShotBindingReferenceComparer();

            private PhysicalShotBindingReferenceComparer()
            {
            }

            public bool Equals(PhysicalShotBinding? left, PhysicalShotBinding? right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(PhysicalShotBinding value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        private sealed class ActiveVisual
        {
            private PhysicalVisualLease _lease;

            internal ActiveVisual(
                long ownerToken,
                Shot? shot,
                PhysicalShotBinding? binding,
                PhysicalProjectileState state,
                PhysicalVisualPose pose,
                bool embedded,
                double expiresAt)
            {
                OwnerToken = ownerToken;
                Shot = shot;
                Binding = binding;
                State = state;
                Pose = pose;
                Embedded = embedded;
                ExpiresAt = expiresAt;
                CurrentPosition = PhysicalImpactGeometryResolver.ToUnity(
                    pose.PositionMetres);
                CurrentOrientation = pose.Orientation;
                DistanceSquaredMetres = double.PositiveInfinity;
            }

            internal long OwnerToken { get; }

            internal Shot? Shot { get; }

            internal PhysicalShotBinding? Binding { get; }

            internal PhysicalProjectileState State { get; }

            internal PhysicalVisualPose Pose { get; }

            internal bool Embedded { get; }

            internal double ExpiresAt { get; }

            internal Vector3 CurrentPosition { get; private set; }

            internal PhysicalOrientation CurrentOrientation { get; private set; }

            internal double DistanceSquaredMetres { get; set; }

            internal string RetirementReason { get; private set; } = "none";

            internal LinkedListNode<long>? OrderNode { get; set; }

            internal bool HasLease { get; private set; }

            internal PhysicalVisualLease Lease
            {
                get { return _lease; }
            }

            internal void SetLease(PhysicalVisualLease lease)
            {
                _lease = lease;
                HasLease = true;
            }

            internal void ClearLease()
            {
                _lease = default;
                HasLease = false;
            }

            internal bool TryUpdatePosition(double now, out Vector3 position)
            {
                position = CurrentPosition;
                if (Embedded)
                {
                    bool remainsActive = now <= ExpiresAt && IsFiniteVector(position);
                    RetirementReason = remainsActive
                        ? "none"
                        : now > ExpiresAt
                            ? "embedded-expired"
                            : "embedded-position-invalid";
                    return remainsActive;
                }

                if (Shot == null
                    || Binding == null
                    || !Binding.Matches(Shot)
                    || !PhysicalShotBindingStore.TryGet(
                        Shot,
                        out PhysicalShotBinding? currentBinding)
                    || !ReferenceEquals(currentBinding, Binding))
                {
                    RetirementReason = "binding-missing-or-recycled";
                    return false;
                }

                Vector3 current = Shot.CurrentPosition;
                Vector3 velocity = Shot.CurrentVelocity;
                if (!IsFiniteVector(current)
                    || !IsFiniteVector(velocity)
                    || velocity.sqrMagnitude <= 0f
                    || !PhysicalOrientation.TryTransport(
                        State.Orientation,
                        State.VelocityMetresPerSecond,
                        PhysicalImpactGeometryResolver.ToPhysical(velocity),
                        out PhysicalOrientation visualOrientation))
                {
                    RetirementReason = "non-finite-or-zero-velocity";
                    return false;
                }

                CurrentPosition = current;
                CurrentOrientation = visualOrientation;
                position = current;
                RetirementReason = "none";
                return true;
            }

            private static bool IsFiniteVector(Vector3 value)
            {
                return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
            }

            private static bool IsFinite(float value)
            {
                return !float.IsNaN(value) && !float.IsInfinity(value);
            }
        }

        private sealed class VisualSlot
        {
            internal VisualSlot(
                GameObject gameObject,
                MeshFilter meshFilter,
                MeshRenderer meshRenderer)
            {
                GameObject = gameObject;
                MeshFilter = meshFilter;
                MeshRenderer = meshRenderer;
            }

            internal GameObject GameObject { get; }

            internal MeshFilter MeshFilter { get; }

            internal MeshRenderer MeshRenderer { get; }

            internal bool IsValid
            {
                get
                {
                    return GameObject != null
                        && MeshFilter != null
                        && MeshRenderer != null;
                }
            }
        }
    }
}
