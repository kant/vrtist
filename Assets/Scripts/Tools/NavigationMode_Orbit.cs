﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRtist
{ 
    public class NavigationMode_Orbit : NavigationMode
    {
        public StraightRay ray = null; // the ray object. Put it somewhere like the StretchUI object.
        public bool limitVertical = true;

        private bool isLocked = false;
        private float minDistance = 0.0f;

        private Transform target = null; // the target object, pointed and gripped by the ray.
        private Vector3 targetPosition = Vector3.zero; // the target object, pointed and gripped by the ray.

        private float scaleSpeed = 0.02f; // percent of scale / frame
        private float moveSpeed = 0.05f;
        private float minMoveDistance = 0.01f;
        private float rotationalSpeed = 3.0f;
        private const float deadZone = 0.5f;

        private float maxPlayerScale = 2000.0f;// world min scale = 0.0005f;
        private float minPlayerScale = 50.0f; // world scale = 50.0f;

        public NavigationMode_Orbit(StraightRay theRay, float speed, float minScale, float maxScale)
        {
            ray = theRay;
            rotationalSpeed = speed;
            minPlayerScale = minScale;
            maxPlayerScale = maxScale;
        }

        public override void Init(Transform rigTransform, Transform worldTransform, Transform leftHandleTransform, Transform pivotTransform, Transform cameraTransform, Transform parametersTransform)
        {
            base.Init(rigTransform, worldTransform, leftHandleTransform, pivotTransform, cameraTransform, parametersTransform);

            // Create tooltips
            Tooltips.CreateTooltip(leftHandle.Find("left_controller").gameObject, Tooltips.Anchors.Joystick, "Turn");
            Tooltips.CreateTooltip(leftHandle.Find("left_controller").gameObject, Tooltips.Anchors.Grip, "Grip Object");

            usedControls = UsedControls.LEFT_GRIP | UsedControls.LEFT_JOYSTICK | UsedControls.RIGHT_JOYSTICK;

            if (ray != null)
            {
                ray.gameObject.SetActive(true);
                ray.SetDefaultColor(); // TODO: does not seem to work.
            }
        }

        public override void DeInit()
        {
            base.DeInit();

            if (ray != null)
            {
                ray.gameObject.SetActive(false);
            }
        }

        public override void Update()
        {
            if (ray == null)
                return;

            //
            // RAY - collision with scene objects.
            //
            if (!isLocked)
            {
                RaycastHit hit;
                Vector3 worldStart = leftHandle.TransformPoint(-0.01f, 0.0f, 0.05f);
                Vector3 worldEnd = leftHandle.TransformPoint(0, 0, 3);
                Vector3 worldDirection = worldEnd - worldStart;
                Ray r = new Ray(worldStart, worldDirection);
                int layersMask = LayerMask.GetMask(new string[] { "Default", "Selection" });
                if (Physics.Raycast(r, out hit, 10.0f, layersMask))
                {

                    target = hit.collider.transform;
                    targetPosition = hit.collider.bounds.center;
                    minDistance = hit.collider.bounds.extents.magnitude;
                    ray.SetStartPosition(worldStart);
                    ray.SetEndPosition(hit.point);
                }
                else
                {
                    target = null;
                    targetPosition = Vector3.zero;
                    minDistance = 0.0f;
                    ray.SetStartPosition(worldStart);
                    ray.SetEndPosition(worldEnd);
                }

                ray.SetDefaultColor(); // because the color change did not work in Init :(
            }
            else 
            {
                Vector3 up = world.up;
                Vector3 forward = Vector3.Normalize(camera.position - targetPosition);
                Vector3 right = Vector3.Cross(up, forward);
                float distance = Vector3.Distance(camera.position, targetPosition);

                //
                // Left Joystick -- left/right = rotate left/right.
                //                  up/down = rotate up/down.
                Vector2 val = VRInput.GetValue(VRInput.leftController, CommonUsages.primary2DAxis);
                if (val != Vector2.zero)
                {
                    // Horizontal rotation
                    if (Mathf.Abs(val.x) > deadZone)
                    {
                        float value = Mathf.Sign(val.x) * (Mathf.Abs(val.x) - deadZone) / (1.0f - deadZone); // remap
                        float rotate_amount_h = value * rotationalSpeed;
                        world.RotateAround(targetPosition, up, rotate_amount_h);
                    }

                    // Vertical rotation
                    if (Mathf.Abs(val.y) > deadZone)
                    {
                        float value = Mathf.Sign(val.y) * (Mathf.Abs(val.y) - deadZone)/ (1.0f - deadZone); // remap
                        float dot = Vector3.Dot(up, forward);
                        bool in_safe_zone = (Mathf.Abs(dot) < 0.8f);
                        bool above_but_going_down = (dot > 0.8f) && (value < 0.0f);
                        bool below_but_going_up = (dot < -0.8f) && (value > 0.0f);
                        if (!limitVertical || in_safe_zone || above_but_going_down || below_but_going_up) // only within limits
                        {
                            float rotate_amount_v = value * rotationalSpeed;
                            world.RotateAround(targetPosition, right, rotate_amount_v);
                        }
                    }
                }

                //
                // Right Joystick -- left/right = move closer/farther
                //                   up/down = scale world
                val = VRInput.GetValue(VRInput.rightController, CommonUsages.primary2DAxis);
                if (val != Vector2.zero)
                {
                    float remainingDistance = distance - minDistance;
                    bool in_safe_zone = (remainingDistance > 0.0f);

                    // Move the world closer/farther
                    if (Mathf.Abs(val.x) > deadZone)
                    {
                        float value = Mathf.Sign(val.x) * (Mathf.Abs(val.x) - deadZone) / (1.0f - deadZone); // remap
                        bool too_close_but_going_back = (remainingDistance <= 0.0f) && (value < 0.0f);
                        if (in_safe_zone || too_close_but_going_back)
                        {
                            Vector3 offset = forward * value * (minMoveDistance + moveSpeed * Mathf.Abs(remainingDistance));
                            world.position += offset;
                            targetPosition += offset;
                        }
                    }

                    // Scale the world
                    if (Mathf.Abs(val.y) > deadZone)
                    {
                        float value = Mathf.Sign(val.y) * (Mathf.Abs(val.y) - deadZone) / (1.0f - deadZone); // remap
                        bool too_close_but_scaling_down = (remainingDistance <= 0.0f) && (value < 0.0f);
                        if (in_safe_zone || too_close_but_scaling_down)
                        {
                            float scale = 1.0f + (value * scaleSpeed);

                            Vector3 scalePivot = targetPosition;
                            Vector3 pivot_to_world = world.position - scalePivot;
                            pivot_to_world.Scale(new Vector3(scale, scale, scale));
                            world.position = scalePivot + pivot_to_world;
                            targetPosition = world.position - pivot_to_world;
                            minDistance *= scale;

                            float finalScale = scale * world.localScale.x;
                            float clampedScale = Mathf.Clamp(finalScale, 1.0f / maxPlayerScale, minPlayerScale);
                            world.localScale = new Vector3(clampedScale, clampedScale, clampedScale);
                            
                            GlobalState.worldScale = world.localScale.x;

                            UpdateCameraClipPlanes();
                        }
                    }
                }

                // Position the ray AFTER the rotation of the camera, to avoid a one frame shift.
                ray.SetStartPosition(leftHandle.TransformPoint(-0.01f, 0.0f, 0.05f));
                ray.SetEndPosition(targetPosition);
            }


            //
            // LEFT GRIP (click) - lock on targetted object/point.
            //

            VRInput.ButtonEvent(VRInput.leftController, CommonUsages.gripButton,
            () =>
            {
                if (target != null)
                {
                    isLocked = true;
                    ray.SetActiveColor();
                }

                GlobalState.IsGrippingWorld = true;
            },
            () =>
            {
                isLocked = false;
                ray.SetDefaultColor();
                GlobalState.IsGrippingWorld = false;
            });
        }
    }
}