using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] Vector3 guardPosition = new Vector3(0.3f, -0.28f, 0.5f);
        [Tooltip("Direction a held weapon points while guarding: up and across the body.")]
        [SerializeField] Vector3 guardDirection = new Vector3(-0.75f, 0.65f, 0.1f);
        [SerializeField] float handSmoothing = 16f;

        [Header("Swing")]
        [SerializeField] Vector3 shoulder = new Vector3(0.18f, -0.28f, 0.05f);
        [SerializeField] float reach = 0.5f;
        [SerializeField] float swingWindup = 0.12f;
        [SerializeField] float swingTime = 0.2f;
        [SerializeField] float swingStartAngle = 80f;
        [SerializeField] float swingEndAngle = -80f;

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
        bool nextSwingFromLeft;
        Vector2 recentMouse;
        Vector3 moveStartPosition;
        Quaternion moveStartRotation;

        Vector3 rightPosition;
        Quaternion rightRotation;
        Vector3 leftPosition;
        Quaternion leftRotation;

        /// <summary>True while mouse-and-keyboard controls are driving the player.</summary>
        public bool Active { get; private set; }

        void Start()
        {
            Active = mode == Mode.ForceDesktop || mode == Mode.Auto && !HeadsetRunning();
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

            if (keyboard.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
            else if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            {
                SetCursorLocked(true);
                return;
            }
            if (keyboard.f1Key.wasPressedThisFrame) showHelp = !showHelp;

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
            // Remember which way the mouse has been moving so a click can swing that way.
            recentMouse = Vector2.Lerp(recentMouse, mouseDelta, 1f - Mathf.Exp(-12f * Time.deltaTime));

            yaw += mouseDelta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -85f, 85f);
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

            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 velocity = origin.rotation * input * speed;

            verticalSpeed = character.isGrounded ? -1f : verticalSpeed + Physics.gravity.y * Time.deltaTime;
            velocity.y = verticalSpeed;
            character.Move(velocity * Time.deltaTime);
        }

        void HandleItems(Keyboard keyboard)
        {
            // E: pick up what you look at, or draw from a holster; with a weapon in hand, put it away.
            if (keyboard.eKey.wasPressedThisFrame)
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
                if (holster.Item == null) continue;
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
                if (holster.Item != null) continue;
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
            if (move != Move.None) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Swing the way the mouse is moving: left, right, or down for an overhead strike.
                if (recentMouse.magnitude > 1.5f)
                {
                    if (Mathf.Abs(recentMouse.y) > Mathf.Abs(recentMouse.x) && recentMouse.y < 0f) swingRoll = 90f;
                    else swingRoll = recentMouse.x > 0f ? 180f : 0f;
                }
                else
                {
                    swingRoll = nextSwingFromLeft ? 180f : 0f;
                }
                nextSwingFromLeft = swingRoll == 0f;
                StartMove(Move.Swing);
            }
            else if (mouse.middleButton.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)
            {
                StartMove(Move.Stab);
            }
        }

        void StartMove(Move next)
        {
            move = next;
            moveTime = 0f;
            moveStartPosition = rightPosition;
            moveStartRotation = rightRotation;
        }

        void UpdateHands(bool guarding)
        {
            float blend = 1f - Mathf.Exp(-handSmoothing * Time.deltaTime);

            if (move == Move.Swing) UpdateSwing();
            else if (move == Move.Stab) UpdateStab();

            if (move == Move.None)
            {
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

            if (moveTime < swingWindup)
            {
                float t = Mathf.SmoothStep(0f, 1f, moveTime / swingWindup);
                SwingPose(swingStartAngle, out Vector3 position, out Quaternion rotation);
                rightPosition = Vector3.Lerp(moveStartPosition, position, t);
                rightRotation = Quaternion.Slerp(moveStartRotation, rotation, t);
            }
            else if (moveTime < swingWindup + swingTime)
            {
                float t = (moveTime - swingWindup) / swingTime;
                SwingPose(Mathf.Lerp(swingStartAngle, endAngle, t), out rightPosition, out rightRotation);
            }
            else
            {
                move = Move.None;
            }
        }

        /// <summary>
        /// Hand pose at one angle of a swing. The swing plane is set by swingRoll (0 = right to left,
        /// 180 = left to right, 90 = overhead). The blade points outwards and its edge leads the swing.
        /// </summary>
        void SwingPose(float angle, out Vector3 position, out Quaternion rotation)
        {
            Vector3 axis = Quaternion.AngleAxis(swingRoll, Vector3.forward) * Vector3.up;
            Vector3 direction = Quaternion.AngleAxis(angle, axis) * Vector3.forward;
            Vector3 travel = -Vector3.Cross(axis, direction);

            position = shoulder + direction * reach;
            rotation = Quaternion.LookRotation(direction, Vector3.Cross(direction, travel));
        }

        void UpdateStab()
        {
            moveTime += Mathf.Min(Time.deltaTime, MaxMoveStep);
            Quaternion aim = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            if (moveTime < stabWindup)
            {
                float t = Mathf.SmoothStep(0f, 1f, moveTime / stabWindup);
                rightPosition = Vector3.Lerp(moveStartPosition, stabStart, t);
                rightRotation = Quaternion.Slerp(moveStartRotation, aim, t);
            }
            else if (moveTime < stabWindup + stabTime)
            {
                float t = (moveTime - stabWindup) / stabTime;
                rightPosition = Vector3.Lerp(stabStart, stabEnd, t);
                rightRotation = aim;
            }
            else if (moveTime >= stabWindup + stabTime + stabHold)
            {
                move = Move.None;
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
            if (Active) SetCursorLocked(false);
        }

        void OnGUI()
        {
            if (!Active) return;

            // Crosshair.
            GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 11f, 20f, 22f), "+");

            if (!showHelp)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 22f), "F1: controls");
                return;
            }

            GUI.Box(new Rect(10f, 10f, 330f, 232f), "Desktop controls (F1 to hide)");
            GUI.Label(new Rect(20f, 34f, 310f, 200f),
                "WASD: move    Shift: sprint\n" +
                "Mouse: look\n" +
                "E: pick up what you look at / draw / put away\n" +
                "G: drop or throw\n" +
                "Left click: swing (flick the mouse left,\n" +
                "    right or down to choose the direction)\n" +
                "Middle click or F: stab\n" +
                "Hold right click: guard (look at the attack)\n" +
                "Q: second hand on / off the weapon\n" +
                "Esc: free the mouse");
        }
    }
}
