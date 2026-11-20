using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BitsPilani.Shared
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BitsPilani/Characters/Character Waypoint Walker")]
    public sealed class CharacterWaypointWalker : MonoBehaviour
    {
        public enum TravelMode { Walk, Run }
        [Serializable]
        public sealed class RoutePoint
        {
            [Tooltip("Destination in the scene. Keep points outside the moving character's hierarchy.")]
            public Transform destination;
            [Tooltip("How to travel TO this point.")] public TravelMode travel = TravelMode.Walk;
            [Tooltip("Random standing time AFTER reaching this point. Set both values to zero to continue immediately.")]
            public Vector2 waitSeconds = Vector2.zero;
            [Tooltip("Optional direction to face while stopped. Empty keeps the arrival direction.")] public Transform lookAt;
            [Tooltip("Optional replacement for this leg's Walk/Run clip.")] public AnimationClip travelClip;
            [Tooltip("Optional replacement for Standing Idle during this point's pause.")] public AnimationClip idleClip;
        }

        [Header("Character and original clips (never modified)")]
        public Animator animator;
        public AnimationClip standingIdle, walk, run;
        [Min(.01f), Tooltip("Crossfade duration when changing animation.")] public float animationBlendSeconds = .25f;
        [Header("Route - ordered scene points")]
        public RoutePoint[] points = Array.Empty<RoutePoint>();
        public bool loop = true;
        [Tooltip("Starts from the character's authored position; never teleports to the first point.")] public bool playOnStart = true;
        [Min(.02f)] public float arrivalDistance = .15f;
        [Header("Movement (metres / seconds)")]
        [Min(.01f)] public float walkSpeed = 1.05f;
        [Min(.01f)] public float runSpeed = 2.1f;
        [Min(.1f)] public float acceleration = 3f;
        [Min(1)] public float turnDegreesPerSecond = 220f;
        [Range(0,.3f), Tooltip("Small per-leg speed variation; zero uses exact speeds.")] public float speedVariation = .06f;
        [Header("Footstep timing - match clips to movement speed")]
        [Min(.01f), Tooltip("Travel speed matching Walk at its original playback rate.")] public float walkClipMetresPerSecond = 1.05f;
        [Min(.01f)] public float runClipMetresPerSecond = 2.1f;
        [Header("Ground following")]
        [Tooltip("Assign the ground Terrain for cheap height sampling. Optional collider fallback supports other ground meshes.")]
        public Terrain groundTerrain;
        public Collider groundCollider;
        [Tooltip("Character root height above the ground, respecting the model's foot/pivot placement.")] public float groundOffset = .008f;
        [Header("Editor helpers and diagnostics")]
        [Min(.5f), Tooltip("Radius used ONLY by Create Example Route in the component's context menu.")] public float exampleRouteRadius = 4f;
        public bool showRoute = true;
        public bool logArrivals;
        public int CurrentPointIndex => pointIndex;
        public bool IsPaused => paused;

        RuntimeAnimatorController savedController;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
        readonly AnimationClip[] slotClips = new AnimationClip[2];
        readonly double[] clocks = new double[2];
        readonly float[] rates = { 1f, 1f };
        int activeSlot = -1, pointIndex;
        float blend = 1, speed, waitRemaining, legVariation = 1;
        bool paused, waiting, finished, savedRootMotion, ready, hasStarted;

        void Reset() { animator = GetComponent<Animator>(); }
        void OnEnable() { paused = !playOnStart; if (hasStarted) Initialize(); }
        void Start() { hasStarted = true; Initialize(); }
        bool Initialize()
        {
            if (ready) return true;
            if (!animator) animator = GetComponent<Animator>();
            if (!animator || !standingIdle || !walk || !run || points == null || points.Length < 2)
            {
                Debug.LogWarning("[CharacterRoute] Assign Animator, Idle/Walk/Run clips and at least two route points: " + name, this);
                return false;
            }
            for (int i = 0; i < points.Length; i++)
                if (points[i] == null || !points[i].destination || points[i].destination.IsChildOf(transform))
                {
                    Debug.LogWarning("[CharacterRoute] Point " + (i + 1) + " is missing or parented to the moving character. Use a separate route parent.", this);
                    return false;
                }
            savedRootMotion = animator.applyRootMotion;
            savedController = animator.runtimeAnimatorController;
            // The route owns animation while enabled. Its original controller must not
            // evaluate a default state over the movement clips later in the frame.
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create(name + " - route animations");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Route animation", animator).SetSourcePlayable(mixer);
            graph.Play();
            ready = true;
            legVariation = UnityEngine.Random.Range(1-speedVariation, 1+speedVariation);
            SelectClip(standingIdle, 1);
            return true;
        }
        public void PauseRoute() { paused = true; speed = 0; }
        public void ResumeRoute() { paused = false; if (!ready) Initialize(); }
        public void RestartRoute()
        {
            pointIndex = 0; waiting = false; finished = false; speed = 0; waitRemaining = 0;
            ResumeRoute(); // Continue from the current position, without teleporting.
        }
        void Update()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            if (!paused && !finished)
            {
                if (waiting)
                {
                    waitRemaining -= dt;
                    var point = points[pointIndex];
                    if (point.lookAt) TurnTowards(point.lookAt.position-transform.position, dt);
                    if (waitRemaining <= 0) AdvancePoint();
                }
                else MoveAlongRoute(dt);
            }
            AnimationClip clip = standingIdle;
            float rate = 1;
            var current = points[pointIndex];
            if (!paused && !finished && !waiting && speed > .04f)
            {
                bool running = current.travel == TravelMode.Run;
                clip = current.travelClip ? current.travelClip : running ? run : walk;
                rate = speed / Mathf.Max(.01f, running ? runClipMetresPerSecond : walkClipMetresPerSecond);
            }
            else if (waiting && current.idleClip) clip = current.idleClip;
            SelectClip(clip, rate);
        }
        void LateUpdate()
        {
            // Apply the chosen pose after Unity's normal Animator update.
            if (ready) EvaluateAnimation(Time.deltaTime);
        }
        void MoveAlongRoute(float dt)
        {
            var point = points[pointIndex];
            if (!point.destination) { PauseRoute(); return; }
            Vector3 delta = point.destination.position-transform.position;
            delta.y = 0;
            float distance = delta.magnitude;
            if (distance <= arrivalDistance)
            {
                float low = Mathf.Max(0, Mathf.Min(point.waitSeconds.x, point.waitSeconds.y));
                float high = Mathf.Max(low, Mathf.Max(point.waitSeconds.x, point.waitSeconds.y));
                waitRemaining = UnityEngine.Random.Range(low, high);
                if (logArrivals) Debug.Log("[CharacterRoute] " + name + " reached " + point.destination.name + "; pause " + waitRemaining.ToString("F1") + "s", this);
                if (waitRemaining > 0) { waiting = true; speed = 0; }
                else AdvancePoint();
                return;
            }
            TurnTowards(delta, dt);
            float angle = Vector3.Angle(transform.forward, delta);
            float turning = Mathf.InverseLerp(95, 15, angle);
            float desired = (point.travel == TravelMode.Run ? runSpeed : walkSpeed)*legVariation*turning;
            if (point.waitSeconds.x > 0 || point.waitSeconds.y > 0 || (!loop && pointIndex == points.Length-1))
                desired = Mathf.Min(desired, Mathf.Sqrt(2*Mathf.Max(.1f, acceleration)*Mathf.Max(.01f, distance-arrivalDistance)));
            speed = Mathf.MoveTowards(speed, desired, Mathf.Max(.1f, acceleration)*dt);
            Vector3 position = transform.position + delta.normalized*Mathf.Min(speed*dt, distance);
            FollowGround(ref position);
            transform.position = position;
        }
        void TurnTowards(Vector3 delta, float dt)
        {
            delta.y = 0;
            if (delta.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), turnDegreesPerSecond*dt);
        }
        void AdvancePoint()
        {
            waiting = false;
            if (pointIndex == points.Length-1 && !loop) { finished = true; speed = 0; return; }
            pointIndex = (pointIndex+1)%points.Length;
            legVariation = UnityEngine.Random.Range(1-speedVariation, 1+speedVariation);
        }
        void FollowGround(ref Vector3 position)
        {
            if (groundTerrain && groundTerrain.terrainData)
            {
                Vector3 local = position-groundTerrain.transform.position;
                Vector3 size = groundTerrain.terrainData.size;
                if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
                {
                    position.y = groundTerrain.SampleHeight(position)+groundTerrain.transform.position.y+groundOffset;
                    return;
                }
            }
            if (groundCollider && groundCollider.Raycast(new Ray(position+Vector3.up*2, Vector3.down), out RaycastHit hit, 4))
                position.y = hit.point.y+groundOffset;
        }
        void SelectClip(AnimationClip clip, float rate)
        {
            if (activeSlot >= 0 && slotClips[activeSlot] == clip) { rates[activeSlot] = rate; return; }
            int next = activeSlot < 0 ? 0 : 1-activeSlot;
            if (slots[next].IsValid()) { mixer.DisconnectInput(next); graph.DestroyPlayable(slots[next]); }
            slots[next] = AnimationClipPlayable.Create(graph, clip);
            slots[next].SetApplyFootIK(false);
            slots[next].SetSpeed(0); // Explicit clocks repeat clips without modifying their asset settings.
            graph.Connect(slots[next], 0, mixer, next);
            slotClips[next] = clip; clocks[next] = 0; rates[next] = rate;
            blend = activeSlot < 0 ? 1 : 0;
            activeSlot = next;
        }
        void EvaluateAnimation(float dt)
        {
            blend = Mathf.MoveTowards(blend, 1, animationBlendSeconds <= 0 ? 1 : dt/animationBlendSeconds);
            float weight = Mathf.SmoothStep(0, 1, blend);
            for (int i = 0; i < 2; i++)
            {
                if (!slots[i].IsValid()) continue;
                clocks[i] += dt*rates[i];
                slots[i].SetTime(clocks[i]%Math.Max(.001, slotClips[i].length));
                mixer.SetInputWeight(i, i == activeSlot ? weight : 1-weight);
            }
            graph.Evaluate(dt);
        }
        void OnDisable()
        {
            if (graph.IsValid()) graph.Destroy();
            if (ready && animator)
            {
                animator.applyRootMotion = savedRootMotion;
                if (!animator.runtimeAnimatorController) animator.runtimeAnimatorController = savedController;
            }
            savedController = null;
            for (int i = 0; i < slots.Length; i++) { slots[i] = default; slotClips[i] = null; clocks[i] = 0; }
            ready = false; activeSlot = -1;
        }
        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
        void OnDrawGizmosSelected()
        {
            if (!showRoute || points == null) return;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null || !points[i].destination) continue;
                Vector3 p = points[i].destination.position;
                Gizmos.color = points[i].travel == TravelMode.Run ? new Color(1,.6f,.1f) : Color.cyan;
                Gizmos.DrawWireSphere(p, .16f);
                int next = (i+1)%points.Length;
                if ((loop || i+1 < points.Length) && points[next] != null && points[next].destination)
                    Gizmos.DrawLine(p, points[next].destination.position);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(p+Vector3.up*.25f, (i+1)+" - "+points[i].travel+" / wait "+points[i].waitSeconds+"s");
#endif
            }
        }
#if UNITY_EDITOR
        [ContextMenu("Create Example Route (empty route only)")]
        void CreateExampleRoute()
        {
            if (Application.isPlaying || (points != null && points.Length > 0)) return;
            UnityEditor.Undo.RecordObject(this, "Create character route");
            var root = new GameObject(name+" - Route (edit points)");
            UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Create character route");
            root.transform.position = transform.position;
            points = new RoutePoint[10];
            Vector3 center = transform.position+transform.forward*exampleRouteRadius;
            for (int i = 0; i < points.Length; i++)
            {
                var node = new GameObject("Point "+(i+1).ToString("00"));
                UnityEditor.Undo.RegisterCreatedObjectUndo(node, "Create route point");
                node.transform.SetParent(root.transform);
                node.transform.position = center+Quaternion.Euler(0,360f*i/points.Length,0)*(-transform.forward*exampleRouteRadius);
                points[i] = new RoutePoint { destination = node.transform };
            }
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
