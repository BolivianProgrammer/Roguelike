using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;
using Roguelike.Weapons;

namespace Roguelike.Player
{
    /// <summary>
    /// Lets the game be played with mouse and keyboard when no headset is running.
    /// It moves the same tracked-pose transforms the headset would, so the physics hands, weapons,
    /// blocking and damage all run exactly the same code as in VR. Swings and thrusts are scripted
    /// hand motions; in VR they are the player's real arms.
    /// </summary>
    public class DesktopRig : MonoBehaviour
    {
        public enum Mode { Auto, ForceDesktop, Disabled }
        enum Move { None, Swing, Stab }
        enum AttackHeight { High, Mid, Low }
        const float MaxMoveStep = 1f / 30f;

        [Tooltip("Auto: desktop controls only when no headset is running. Force Desktop: always, for testing.")]
        [SerializeField] Mode mode = Mode.Auto;

        [Header("Rig")]
        [SerializeField] Transform origin;
        [SerializeField] Transform cameraOffset;
        [SerializeField] Camera head;
        [SerializeField] CharacterController character;
        [Tooltip("The transforms the physics hands follow (the tracked grip poses).")]
        [SerializeField] Transform leftHandTarget;
        [SerializeField] Transform rightHandTarget;
        [SerializeField] PhysicsHandGrabber leftGrabber;
        [SerializeField] PhysicsHandGrabber rightGrabber;
        [Tooltip("Components that only make sense with a headset (tracked pose drivers, stick locomotion).")]
        [SerializeField] Behaviour[] disableOnDesktop;
        [Tooltip("Objects that only make sense with a headset (controller models, ray interactors).")]
        [SerializeField] GameObject[] hideOnDesktop;

        [Header("Look and move")]
        [SerializeField] float eyeHeight = 1.65f;
        [SerializeField] float fieldOfView = 75f;
        [Tooltip("Degrees of turn per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float walkSpeed = 2.5f;
        [SerializeField] float sprintSpeed = 4.5f;
        [SerializeField] float physicsRate = 90f;

        [Header("Hands (positions relative to the camera)")]
        [SerializeField] Vector3 rightRestPosition = new Vector3(0.28f, -0.32f, 0.42f);
        [SerializeField] Vector3 leftRestPosition = new Vector3(-0.28f, -0.32f, 0.42f);
        [Tooltip("Direction a held weapon points while idle.")]
        [SerializeField] Vector3 restDirection = new Vector3(-0.1f, 0.8f, 0.6f);
        [Tooltip("Where the hand holds the weapon while guarding. Near the middle of the chest, so attacks from either side meet the blade.")]
        [SerializeField] Vector3 guardPosition = new Vector3(0.12f, -0.3f, 0.45f);
        [Tooltip("Direction a held weapon points while guarding: across the chest, tilted up.")]
        [SerializeField] Vector3 guardDirection = new Vector3(-0.85f, 0.5f, 0.15f);
        [SerializeField] float handSmoothing = 16f;

        [Header("Swing")]
        [SerializeField] Vector3 shoulder = new Vector3(0.18f, -0.28f, 0.05f);
        [SerializeField] float reach = 0.5f;
        [SerializeField] float swingWindup = 0.12f;
        [SerializeField] float swingTime = 0.2f;
        [SerializeField] float swingStartAngle = 80f;
        [SerializeField] float swingEndAngle = -80f;

        [Tooltip("How much each kilogram of weapon slows the swing and stab down.")]
        [SerializeField] float weightSlowdownPerKg = 0.12f;

        [Header("Attack height (Z / X / C)")]
        [Tooltip("How far above chest level (meters) high attacks are aimed, to reach the head.")]
        [SerializeField] float highOffset = 0.22f;
        [Tooltip("How far below chest level (meters) low attacks are aimed, to reach the legs.")]
        [SerializeField] float lowOffset = -0.75f;

        [Header("Heavy attack (hold the attack button)")]
        [Tooltip("Seconds the button must be held for the swing to become a heavy attack.")]
        [SerializeField] float heavyChargeTime = 0.45f;
        [Tooltip("How far back a heavy attack is drawn before the swing.")]
        [SerializeField] float heavyStartAngle = 115f;
        [SerializeField] float heavyDamageScale = 1.6f;
        [SerializeField] float heavyStaggerScale = 2f;
        [Tooltip("A heavy attack costs this many times the weapon's normal stamina per hit.")]
        [SerializeField] float heavyStaminaScale = 2.5f;

        [Header("Stab")]
        [SerializeField] Vector3 stabStart = new Vector3(0.2f, -0.22f, 0.15f);
        [SerializeField] Vector3 stabEnd = new Vector3(0.08f, -0.15f, 0.8f);
        [SerializeField] float stabWindup = 0.1f;
        [SerializeField] float stabTime = 0.1f;
        [SerializeField] float stabHold = 0.08f;

        float yaw;
        float pitch;
        float verticalSpeed;
        bool showHelp = true;

        Move move;
        float moveTime;
        float swingRoll;
        // Left-click combo: which side the blade finished on, the last attack picked, and when.
        ComboSide comboSide = ComboSide.Right;
        int lastCombo = -1;
        float lastAttackAt = -10f;
        AttackHeight attackHeight = AttackHeight.Mid;
        ButtonControl chargeButton;
        bool charging;
        float chargeTime;
        bool heavy;
        float weightScale = 1f;
        Vector3 moveStartPosition;
        Quaternion moveStartRotation;

        Vector3 rightPosition;
        Quaternion rightRotation;
        Vector3 leftPosition;
        Quaternion leftRotation;

        /// <summary>True while mouse-and-keyboard controls are driving the player.</summary>
        public bool Active { get; private set; }

        /// <summary>True when the game is being played with mouse and keyboard rather than a headset.</summary>
        public static bool IsDesktop { get; private set; }

        /// <summary>
        /// Set while a menu is open: the mouse is freed to point at it, and looking, attacking and item keys
        /// stand down. Walking still works, so the player can simply walk away from a menu.
        /// </summary>
        public static bool InputBlocked { get; set; }

        bool wasBlocked;

        void Start()
        {
            Active = mode == Mode.ForceDesktop || mode == Mode.Auto && !HeadsetRunning();
            // With a mouse the weapon also moves whenever the camera turns, so only deliberate attacks may hurt.
            Weapon.RequireAttackWindow = Active;
            IsDesktop = Active;
            InputBlocked = false;
            if (!Active)
            {
                enabled = false;
                return;
            }

            if (disableOnDesktop != null)
                foreach (var behaviour in disableOnDesktop)
                    if (behaviour != null) behaviour.enabled = false;
            if (hideOnDesktop != null)
                foreach (var go in hideOnDesktop)
                    if (go != null) go.SetActive(false);

            Time.fixedDeltaTime = 1f / physicsRate;
            head.fieldOfView = fieldOfView;
            head.transform.localPosition = Vector3.zero;
            cameraOffset.localPosition = new Vector3(0f, eyeHeight, 0f);
            yaw = origin.eulerAngles.y;

            rightPosition = rightRestPosition;
            leftPosition = leftRestPosition;
            rightRotation = leftRotation = Quaternion.LookRotation(restDirection.normalized, Vector3.back);
            ApplyHands();

            // On desktop you grab what you look at, not what your fist points at.
            leftGrabber.AimOverride = head.transform;
            rightGrabber.AimOverride = head.transform;

            SetCursorLocked(true);
        }

        static bool HeadsetRunning()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays)
                if (display.running) return true;
            return false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (InputBlocked)
            {
                if (!wasBlocked) SetCursorLocked(false);
                wasBlocked = true;
                charging = false;
                Walk(keyboard);
                UpdateHands(false);
                ApplyHands();
                return;
            }
            if (wasBlocked)
            {
                wasBlocked = false;
                SetCursorLocked(true);
            }

            // Esc opens the pause menu (which frees the mouse); a click takes the mouse back afterwards.
            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
                return;
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                Meta.GameSettings.Current.showKeyHints = !Meta.GameSettings.Current.showKeyHints;
                Meta.GameSettings.Save();
            }
            showHelp = Meta.GameSettings.Current.showKeyHints;

            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Vector2 mouseDelta = locked ? mouse.delta.ReadValue() : Vector2.zero;

            Look(mouseDelta);
            Walk(keyboard);

            if (locked)
            {
                HandleItems(keyboard);
                HandleAttacks(keyboard, mouse);
            }

            UpdateHands(locked && mouse.rightButton.isPressed);
            ApplyHands();
        }

        void Look(Vector2 mouseDelta)
        {
            float sensitivity = Meta.GameSettings.Current.mouseSensitivity > 0f ? Meta.GameSettings.Current.mouseSensitivity : mouseSensitivity;
            yaw += mouseDelta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, -85f, 85f);
            origin.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Walk(Keyboard keyboard)
        {
            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector3.ClampMagnitude(input, 1f);

            float speed = (keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed) * PlayerStats.Get(StatType.MoveSpeed);
            Vector3 velocity = origin.rotation * input * speed;

            verticalSpeed = character.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalSpeed;
            character.Move(velocity * Time.deltaTime);
        }

        void HandleItems(Keyboard keyboard)
        {
            // E: pick up what you look at, or draw from a holster; with a weapon in hand, put it away.
            if (keyboard.eKey.wasPressedThisFrame && Hub.HubNpc.Focused == null && UI.InteractPrompt.Focused == null)
            {
                if (rightGrabber.Held != null) PutAway();
                else if (!rightGrabber.TryGrab()) DrawFromHolster();
            }

            // G: drop (or throw, if pressed mid-swing).
            if (keyboard.gKey.wasPressedThisFrame)
            {
                leftGrabber.Release();
                rightGrabber.Release();
            }

            // R: the held weapon's special attack.
            if (keyboard.rKey.wasPressedThisFrame) rightGrabber.ActivateHeldSpecial();

            // Q: second hand on or off the weapon.
            if (keyboard.qKey.wasPressedThisFrame)
            {
                if (leftGrabber.Held != null) leftGrabber.Release();
                else if (rightGrabber.Held != null) leftGrabber.GrabItem(rightGrabber.Held);
            }
        }

        void DrawFromHolster()
        {
            foreach (var holster in FindObjectsOfType<Holster>())
            {
                // Only what is worn; weapons on a rack are picked up by looking at them.
                if (holster.Item == null || !holster.OnBody) continue;
                rightGrabber.GrabItem(holster.Item);
                return;
            }
        }

        void PutAway()
        {
            Grabbable item = rightGrabber.Held;
            Holster free = null;
            foreach (var holster in FindObjectsOfType<Holster>())
            {
                if (holster.Item != null || !holster.OnBody) continue;
                free = holster;
                break;
            }

            leftGrabber.Release();
            rightGrabber.Release();
            // Released next to a holster it may already have been stored; otherwise store it ourselves.
            if (free != null && item.Holster == null) free.Store(item);
        }

        void HandleAttacks(Keyboard keyboard, Mouse mouse)
        {
            // No weapon in hand, no attack.
            if (rightGrabber.Held == null)
            {
                charging = false;
                return;
            }
            if (move != Move.None) return;

            // Z / X / C: aim attacks at head, chest or leg height.
            if (keyboard.zKey.wasPressedThisFrame) attackHeight = AttackHeight.High;
            if (keyboard.xKey.wasPressedThisFrame) attackHeight = AttackHeight.Mid;
            if (keyboard.cKey.wasPressedThisFrame) attackHeight = AttackHeight.Low;

            if (!charging && mouse.leftButton.wasPressedThisFrame)
            {
                // Left click: the next attack of a natural combo, picked at random (see NextComboAttack).
                ComboAttack next = NextComboAttack();
                if (next.stab)
                {
                    heavy = false;
                    StartMove(Move.Stab);
                }
                else
                {
                    swingRoll = next.roll;
                    BeginCharge(mouse.leftButton);
                }
            }
            else if (!charging && TryDirectionKey(keyboard))
            {
                // A number key picked an exact swing direction.
            }
            else if (charging)
            {
                // Holding the button draws the weapon back; letting go swings. A long hold is a heavy attack.
                chargeTime += Time.deltaTime;
                if (chargeButton == null || !chargeButton.isPressed)
                {
                    charging = false;
                    heavy = chargeTime >= heavyChargeTime && PlayerStamina.Has(HeldStaminaCost() * heavyStaminaScale);
                    StartMove(Move.Swing);
                }
            }
            else if (mouse.middleButton.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            {
                heavy = false;
                StartMove(Move.Stab);
            }
        }

        // ---------------------------------------------------------------- Left-click combo

        enum ComboSide { Right, Left, Centre }

        /// <summary>One attack the left click can pick: a swing (its roll) or the stab, and where the blade starts and ends.</summary>
        struct ComboAttack
        {
            public float roll;
            public bool stab;
            public ComboSide from, to;
            public float weight;

            public ComboAttack(float roll, ComboSide from, ComboSide to, float weight, bool stab = false)
            {
                this.roll = roll;
                this.from = from;
                this.to = to;
                this.weight = weight;
                this.stab = stab;
            }
        }

        /// <summary>Every attack the desktop controls have (the same set as the number keys and the stab).</summary>
        static readonly ComboAttack[] ComboAttacks =
        {
            new ComboAttack(0f, ComboSide.Right, ComboSide.Left, 3f),      // right to left
            new ComboAttack(180f, ComboSide.Left, ComboSide.Right, 3f),    // left to right (backhand)
            new ComboAttack(45f, ComboSide.Right, ComboSide.Left, 2.5f),   // diagonal down from the right
            new ComboAttack(135f, ComboSide.Left, ComboSide.Right, 2.5f),  // diagonal down from the left
            new ComboAttack(90f, ComboSide.Centre, ComboSide.Centre, 2f),  // overhead
            new ComboAttack(270f, ComboSide.Centre, ComboSide.Centre, 1f), // rising cut
            new ComboAttack(0f, ComboSide.Centre, ComboSide.Centre, 1.5f, stab: true),
        };

        /// <summary>Seconds without attacking after which the combo starts again from a ready stance.</summary>
        const float ComboReset = 1.4f;

        /// <summary>
        /// Picks the left click's next attack at random, but so a chain of them flows like real swordplay: each
        /// one starts where the last left the blade (a cut that ends on the left is followed by one from the left,
        /// an overhead or a thrust), never the same attack twice in a row, and after a pause it starts again from
        /// the ready stance (blade on the right).
        /// </summary>
        ComboAttack NextComboAttack()
        {
            if (Time.time - lastAttackAt > ComboReset)
            {
                comboSide = ComboSide.Right;
                lastCombo = -1;
            }

            float total = 0f;
            for (int i = 0; i < ComboAttacks.Length; i++)
                if (Flows(i)) total += ComboAttacks[i].weight;

            float pick = Random.value * total;
            int chosen = 0;
            for (int i = 0; i < ComboAttacks.Length; i++)
            {
                if (!Flows(i)) continue;
                chosen = i;
                pick -= ComboAttacks[i].weight;
                if (pick <= 0f) break;
            }

            lastCombo = chosen;
            lastAttackAt = Time.time;
            // After an overhead, a rising cut or a thrust the blade is back in the middle: either side can follow.
            comboSide = ComboAttacks[chosen].to;
            return ComboAttacks[chosen];
        }

        /// <summary>True if attack i can follow the last one: not a repeat, and starting where the blade is.</summary>
        bool Flows(int i)
        {
            if (i == lastCombo) return false;
            ComboSide from = ComboAttacks[i].from;
            return from == ComboSide.Centre || comboSide == ComboSide.Centre || from == comboSide;
        }

        /// <summary>
        /// Number keys swing in one exact direction each, for testing where hits land:
        /// 1 right to left, 2 left to right, 3 overhead, 4 upward, 5 diagonal down from the right, 6 diagonal down from the left.
        /// </summary>
        bool TryDirectionKey(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) return BeginDirected(keyboard.digit1Key, 0f);
            if (keyboard.digit2Key.wasPressedThisFrame) return BeginDirected(keyboard.digit2Key, 180f);
            if (keyboard.digit3Key.wasPressedThisFrame) return BeginDirected(keyboard.digit3Key, 90f);
            if (keyboard.digit4Key.wasPressedThisFrame) return BeginDirected(keyboard.digit4Key, 270f);
            if (keyboard.digit5Key.wasPressedThisFrame) return BeginDirected(keyboard.digit5Key, 45f);
            if (keyboard.digit6Key.wasPressedThisFrame) return BeginDirected(keyboard.digit6Key, 135f);
            return false;
        }

        bool BeginDirected(ButtonControl button, float roll)
        {
            swingRoll = roll;
            BeginCharge(button);
            return true;
        }

        void BeginCharge(ButtonControl button)
        {
            chargeButton = button;
            charging = true;
            chargeTime = 0f;
        }

        /// <summary>How far above or below chest level attacks are aimed, in meters.</summary>
        float HeightOffset()
        {
            switch (attackHeight)
            {
                case AttackHeight.High: return highOffset;
                case AttackHeight.Low: return lowOffset;
                default: return 0f;
            }
        }

        void StartMove(Move next)
        {
            move = next;
            moveTime = 0f;
            moveStartPosition = rightPosition;
            moveStartRotation = rightRotation;

            // Heavier weapons take longer to swing.
            weightScale = 1f + rightGrabber.Held.Body.mass * weightSlowdownPerKg;

            // Weapons can only hurt during the strike itself. The wind-up (bringing the blade back into position)
            // must not: it moves fast, often with the flat or the back of the blade, and can pass through the enemy.
            windowPending = true;
            windowDuration = next == Move.Swing
                ? swingTime * weightScale
                : stabTime * weightScale + stabHold;
        }

        bool windowPending;
        float windowDuration;

        /// <summary>Called when the wind-up ends and the strike starts.</summary>
        void OpenStrikeWindow()
        {
            if (!windowPending) return;
            windowPending = false;
            if (heavy) Weapon.OpenAttackWindow(windowDuration + 0.15f, heavyDamageScale, heavyStaggerScale, heavyStaminaScale);
            else Weapon.OpenAttackWindow(windowDuration + 0.15f);
        }

        float HeldStaminaCost()
        {
            var weapon = rightGrabber.Held != null ? rightGrabber.Held.GetComponent<Weapon>() : null;
            return weapon != null && weapon.Definition != null ? weapon.Definition.StaminaCost * PlayerStats.Get(StatType.StaminaCost) : 0f;
        }

        void UpdateHands(bool guarding)
        {
            float blend = 1f - Mathf.Exp(-handSmoothing * Time.deltaTime);

            if (move == Move.Swing) UpdateSwing();
            else if (move == Move.Stab) UpdateStab();

            if (move == Move.None && charging)
            {
                // Drawing back for a swing: further back the longer the button is held.
                float drawn = Mathf.Clamp01(chargeTime / heavyChargeTime);
                SwingPose(Mathf.Lerp(swingStartAngle * 0.6f, heavyStartAngle, drawn), out Vector3 drawPosition, out Quaternion drawRotation);
                rightPosition = Vector3.Lerp(rightPosition, drawPosition, blend);
                rightRotation = Quaternion.Slerp(rightRotation, drawRotation, blend);
            }
            else if (move == Move.None)
            {
                // A broken guard drops the weapon out of the way for a moment.
                guarding &= rightGrabber.Held != null && !Combat.PlayerGuard.Broken;
                Vector3 goalPosition = guarding ? guardPosition : rightRestPosition;
                Vector3 goalDirection = (guarding ? guardDirection : restDirection).normalized;
                Quaternion goalRotation = Quaternion.LookRotation(goalDirection, Vector3.back);
                rightPosition = Vector3.Lerp(rightPosition, goalPosition, blend);
                rightRotation = Quaternion.Slerp(rightRotation, goalRotation, blend);
            }

            // The left hand idles at the hip, or sits just below the right hand on a two-handed grip.
            bool twoHanded = leftGrabber.Held != null && leftGrabber.Held == rightGrabber.Held;
            if (twoHanded)
            {
                leftPosition = rightPosition - rightRotation * Vector3.forward * 0.14f;
                leftRotation = rightRotation;
            }
            else
            {
                leftPosition = Vector3.Lerp(leftPosition, leftRestPosition, blend);
                leftRotation = Quaternion.Slerp(leftRotation, Quaternion.LookRotation(restDirection.normalized, Vector3.back), blend);
            }
        }

        void UpdateSwing()
        {
            // Capped so a frame hitch cannot skip the whole swing.
            moveTime += Mathf.Min(Time.deltaTime, MaxMoveStep);
            // Overhead strikes stop short so the blade does not bury itself in the floor.
            float endAngle = Mathf.Approximately(swingRoll, 90f) ? swingEndAngle * 0.6f : swingEndAngle;
            float startAngle = heavy ? heavyStartAngle : swingStartAngle;
            float windup = swingWindup * weightScale;
            float swing = swingTime * weightScale;

            if (moveTime < windup)
            {
                float t = Mathf.SmoothStep(0f, 1f, moveTime / windup);
                SwingPose(startAngle, out Vector3 position, out Quaternion rotation);
                rightPosition = Vector3.Lerp(moveStartPosition, position, t);
                rightRotation = Quaternion.Slerp(moveStartRotation, rotation, t);
            }
            else if (moveTime < windup + swing)
            {
                OpenStrikeWindow();
                float t = (moveTime - windup) / swing;
                SwingPose(Mathf.Lerp(startAngle, endAngle, t), out rightPosition, out rightRotation);
            }
            else
            {
                move = Move.None;
                windowPending = false;
            }
        }

        /// <summary>
        /// Hand pose at one angle of a swing. The swing plane is set by swingRoll (0 = right to left,
        /// 180 = left to right, 90 = overhead, 270 = upward, 45 and 135 = diagonals).
        /// The blade points outwards and its edge leads the swing.
        /// </summary>
        void SwingPose(float angle, out Vector3 position, out Quaternion rotation)
        {
            Vector3 axis = Quaternion.AngleAxis(swingRoll, Vector3.forward) * Vector3.up;
            Vector3 direction = Quaternion.AngleAxis(angle, axis) * Vector3.forward;
            Vector3 travel = -Vector3.Cross(axis, direction);

            position = shoulder + Vector3.up * HeightOffset() + direction * reach;
            rotation = Quaternion.LookRotation(direction, Vector3.Cross(direction, travel));
        }

        void UpdateStab()
        {
            moveTime += Mathf.Min(Time.deltaTime, MaxMoveStep);
            Quaternion aim = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            float windup = stabWindup * weightScale;
            float thrust = stabTime * weightScale;
            Vector3 height = Vector3.up * HeightOffset();
            Vector3 stabStart = this.stabStart + height;
            Vector3 stabEnd = this.stabEnd + height;

            if (moveTime < windup)
            {
                float t = Mathf.SmoothStep(0f, 1f, moveTime / windup);
                rightPosition = Vector3.Lerp(moveStartPosition, stabStart, t);
                rightRotation = Quaternion.Slerp(moveStartRotation, aim, t);
            }
            else if (moveTime < windup + thrust)
            {
                OpenStrikeWindow();
                float t = (moveTime - windup) / thrust;
                rightPosition = Vector3.Lerp(stabStart, stabEnd, t);
                rightRotation = aim;
            }
            else if (moveTime >= windup + thrust + stabHold)
            {
                move = Move.None;
                windowPending = false;
            }
        }

        void ApplyHands()
        {
            Transform view = head.transform;
            rightHandTarget.SetPositionAndRotation(view.TransformPoint(rightPosition), view.rotation * rightRotation);
            leftHandTarget.SetPositionAndRotation(view.TransformPoint(leftPosition), view.rotation * leftRotation);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnDisable()
        {
            if (!Active) return;
            SetCursorLocked(false);
            Weapon.RequireAttackWindow = false;
            IsDesktop = false;
            InputBlocked = false;
        }

        // ---------------------------------------------------------------- Screen HUD

        /// <summary>The controls card. Keys in gold, actions in the body colour.</summary>
        const string ControlsText =
            "<color=#FFD25A>WASD</color>  move    <color=#FFD25A>Shift</color>  sprint    <color=#FFD25A>Mouse</color>  look\n" +
            "<color=#FFD25A>E</color>  pick up / draw / put away / talk\n" +
            "<color=#FFD25A>G</color>  drop or throw\n" +
            "<color=#FFD25A>Left click</color>  attack <color=#9C958A>(each click flows into the next)</color>\n" +
            "<color=#9C958A>Exact swings:</color>  <color=#FFD25A>1 2</color>  sideways    <color=#FFD25A>3 4</color>  overhead / upward\n" +
            "<color=#FFD25A>5 6</color>  diagonals\n" +
            "<color=#FFD25A>Z X C</color>  aim high / mid / low\n" +
            "<color=#FFD25A>Hold</color> an attack, release  heavy\n" +
            "<color=#FFD25A>Middle click</color> or <color=#FFD25A>F</color>  stab\n" +
            "<color=#FFD25A>Hold right click</color>  guard <color=#9C958A>(look at the attack)</color>\n" +
            "<color=#FFD25A>R</color>  weapon special    <color=#FFD25A>Q</color>  two-handed grip\n" +
            "<color=#FFD25A>Esc</color>  pause";

        float nextHudText;

        /// <summary>Crosshair, controls card, and the combat / weapon lines at the bottom of the screen.</summary>
        void LateUpdate()
        {
            if (!Active) return;
            UI.DesktopHud hud = UI.DesktopHud.Get();
            if (hud == null) return;
            hud.SetCrosshair(!InputBlocked);
            hud.SetHelp(showHelp, ControlsText);

            // The lines change at most a few times a second; build their strings at that pace, not every frame.
            if (Time.unscaledTime < nextHudText) return;
            nextHudText = Time.unscaledTime + 0.1f;
            hud.SetCombat(CombatLine());
            hud.SetWeapon(WeaponLine());
        }

        string CombatLine()
        {
            string line = "Aim  <color=#DDD2BA>" + attackHeight + "</color>";
            if (charging && chargeTime >= heavyChargeTime) line += "    <color=#FF6A3D><b>HEAVY</b></color>";
            if (!string.IsNullOrEmpty(Enemies.EnemyHealth.LastHit)) line += "    <color=#9C958A>Last hit  " + Enemies.EnemyHealth.LastHit + "</color>";
            return line;
        }

        string WeaponLine()
        {
            if (rightGrabber.Held == null) return null;
            var weapon = rightGrabber.Held.GetComponent<Weapon>();
            if (weapon == null) return null;
            string name = "<b>" + weapon.Definition.displayName + "</b>";
            if (weapon.Special == null) return name;

            var special = weapon.Special;
            string status = special.IsActive ? "<color=#FFD25A><b>ACTIVE</b></color>"
                : special.IsReady ? "<color=#FFD25A>READY</color>  <color=#9C958A>[R]</color>"
                : "<color=#9C958A>" + Mathf.CeilToInt(special.CooldownRemaining) + "s</color>";
            return name + "   <color=#D4A82A>|</color>   " + special.DisplayName + "  " + status;
        }
    }
}
