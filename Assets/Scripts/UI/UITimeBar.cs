﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEditor;
using UnityEngine.UI;
using System;

namespace VRtist
{
    [ExecuteInEditMode]
    [SelectionBase]
    [RequireComponent(typeof(MeshFilter)),
     RequireComponent(typeof(MeshRenderer)),
     RequireComponent(typeof(BoxCollider))]
    public class UITimeBar : UIElement
    {
        [SpaceHeader("TimeBar Base Shape Parmeters", 6, 0.8f, 0.8f, 0.8f)]
        [CentimeterFloat] public float thickness = 0.001f;
        public Color pushedColor = new Color(0.3f, 0.3f, 0.3f);

        [SpaceHeader("TimeBar SubComponents Shape Parameters", 6, 0.8f, 0.8f, 0.8f)]
        [CentimeterFloat] public float knobHeadWidth = 0.002f;
        [CentimeterFloat] public float knobHeadHeight = 0.013f;
        [CentimeterFloat] public float knobHeadDepth = 0.003f;
        [CentimeterFloat] public float knobFootWidth = 0.001f;
        [CentimeterFloat] public float knobFootHeight = 0.005f;
        [CentimeterFloat] public float knobFootDepth = 0.001f; // == railThickness
        // TODO: add colors here?

        [SpaceHeader("TimeBar Values", 6, 0.8f, 0.8f, 0.8f)]
        public int minValue = 0;
        public int maxValue = 250;
        public int currentValue = 0;
        

        // TODO: type? handle int and float.
        //       precision, step?

        [SpaceHeader("Callbacks", 6, 0.8f, 0.8f, 0.8f)]
        public IntChangedEvent onSlideEvent = new IntChangedEvent(); // TODO: maybe make 2 callbacks, one for floats, one for ints
        public UnityEvent onClickEvent = null;
        public UnityEvent onReleaseEvent = null;

        [SerializeField] private UITimeBarKnob knob = null;

        private bool needRebuild = false;

        public int Value { get { return GetValue(); } set { SetValue(value); UpdateTimeBarPosition(); } }

        void Start()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
#else
            if (Application.isPlaying)
#endif
            {
                onSlideEvent.AddListener(OnSlide);
                onClickEvent.AddListener(OnClickTimeBar);
                onReleaseEvent.AddListener(OnReleaseTimeBar);
            }
        }

        public override void RebuildMesh()
        {
            // TIME TICKS ??
            // ...

            // KNOB
            float newKnobHeadWidth = knobHeadWidth;
            float newKnobHeadHeight = knobHeadHeight;
            float newKnobHeadDepth = knobHeadDepth;
            float newKnobFootWidth = knobFootWidth;
            float newKnobFootHeight = knobFootHeight;
            float newKnobFootDepth = knobFootDepth;

            knob.RebuildMesh(newKnobHeadWidth, newKnobHeadHeight, newKnobHeadDepth, newKnobFootWidth, newKnobFootHeight, newKnobFootDepth);
            
            // BASE
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            Mesh theNewMesh = UIUtils.BuildBoxEx(width, height, thickness);
            theNewMesh.name = "UITimeBar_GeneratedMesh";
            meshFilter.sharedMesh = theNewMesh;

            UpdateColliderDimensions();
            UpdateTimeBarPosition();
        }

        private void UpdateColliderDimensions()
        {
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            BoxCollider coll = gameObject.GetComponent<BoxCollider>();
            if (meshFilter != null && coll != null)
            {
                Vector3 initColliderCenter = meshFilter.sharedMesh.bounds.center;
                Vector3 initColliderSize = meshFilter.sharedMesh.bounds.size;
                if (initColliderSize.z < UIElement.collider_min_depth_shallow)
                {
                    coll.center = new Vector3(initColliderCenter.x, initColliderCenter.y, UIElement.collider_min_depth_shallow / 2.0f);
                    coll.size = new Vector3(initColliderSize.x, initColliderSize.y, UIElement.collider_min_depth_shallow);
                }
                else
                {
                    coll.center = initColliderCenter;
                    coll.size = initColliderSize;
                }
            }
        }

        private void OnValidate()
        {
            const float min_width = 0.01f;
            const float min_height = 0.01f;
            const float min_thickness = 0.001f;

            if (width < min_width)
                width = min_width;
            if (height < min_height)
                height = min_height;
            if (thickness < min_thickness)
                thickness = min_thickness;
            if (currentValue < minValue)
                currentValue = minValue;
            if (currentValue > maxValue)
                currentValue = maxValue;

            needRebuild = true;
        }

        private void Update()
        {
            // NOTE: rebuild when touching a property in the inspector.
            // Boolean needRebuild is set in OnValidate();
            // The rebuild method called when using the gizmos is: Width and Height
            // properties in UIElement.
            // This comment is probably already obsolete.
            if (needRebuild)
            {
                // NOTE: I do all these things because properties can't be called from the inspector.
                try
                {
                    RebuildMesh();
                    UpdateLocalPosition();
                    UpdateAnchor();
                    UpdateChildren();
                    UpdateTimeBarPosition();
                    SetColor(baseColor);
                }
                catch(Exception e)
                {
                    Debug.Log("Exception: " + e);
                }

                needRebuild = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 labelPosition = transform.TransformPoint(new Vector3(width / 4.0f, -height / 2.0f, -0.001f));
            Vector3 posTopLeft = transform.TransformPoint(new Vector3(0.0f, 0.0f, -0.001f));
            Vector3 posTopRight = transform.TransformPoint(new Vector3(width, 0.0f, -0.001f));
            Vector3 posBottomLeft = transform.TransformPoint(new Vector3(0.0f, -height, -0.001f));
            Vector3 posBottomRight = transform.TransformPoint(new Vector3(width, -height, -0.001f));

            Gizmos.color = Color.white;
            Gizmos.DrawLine(posTopLeft, posTopRight);
            Gizmos.DrawLine(posTopRight, posBottomRight);
            Gizmos.DrawLine(posBottomRight, posBottomLeft);
            Gizmos.DrawLine(posBottomLeft, posTopLeft);

#if UNITY_EDITOR
            Gizmos.color = Color.white;
            UnityEditor.Handles.Label(labelPosition, gameObject.name);
#endif
        }

        private void UpdateTimeBarPosition()
        {
            float pct = (float)currentValue / (float)(maxValue - minValue);

            float startX = 0.0f;
            float endX = width;
            float posX = startX + pct * (endX - startX);

            Vector3 knobPosition = new Vector3(posX, -height, 0.0f); // knob is at the bottom of the widget

            knob.transform.localPosition = knobPosition;
        }

        private int GetValue()
        {
            return currentValue;
        }

        private void SetValue(int intValue)
        {
            currentValue = intValue;
        }

        private void OnTriggerEnter(Collider otherCollider)
        {
            if (otherCollider.gameObject.name == "Cursor")
            {
                // HIDE cursor

                onClickEvent.Invoke();
            }
        }

        private void OnTriggerExit(Collider otherCollider)
        {
            if (otherCollider.gameObject.name == "Cursor")
            {
                // SHOW cursor

                onReleaseEvent.Invoke();
            }
        }

        private void OnTriggerStay(Collider otherCollider)
        {
            if (otherCollider.gameObject.name == "Cursor")
            {
                // NOTE: The correct "currentValue" is already computed in the HandleCursorBehavior callback.
                //       Just call the listeners here.
                onSlideEvent.Invoke(currentValue);
            }
        }

        public void OnClickTimeBar()
        {
            SetColor(pushedColor);
        }

        public void OnReleaseTimeBar()
        {
            SetColor(baseColor);
        }

        public void OnSlide(int f)
        {
            //Value = f; // Value already set in HandleCursorBehavior
        }

        public override bool HandlesCursorBehavior() { return true; }
        public override void HandleCursorBehavior(Vector3 worldCursorColliderCenter, ref Transform cursorShapeTransform)
        {
            Vector3 localWidgetPosition = transform.InverseTransformPoint(worldCursorColliderCenter);
            Vector3 localProjectedWidgetPosition = new Vector3(localWidgetPosition.x, localWidgetPosition.y, 0.0f);

            float startX = 0.0f;
            float endX = width;
            float snapXDistance = 0.002f; // for snapping of a little bit to the right/left of extremities
            if (localProjectedWidgetPosition.x > startX - snapXDistance && localProjectedWidgetPosition.x < endX + snapXDistance)
            {
                // SNAP X left
                if (localProjectedWidgetPosition.x < startX)
                    localProjectedWidgetPosition.x = startX;

                // SNAP X right
                if(localProjectedWidgetPosition.x > endX)
                    localProjectedWidgetPosition.x = endX;

                // Compute closest int for snapping.
                float pct = (localProjectedWidgetPosition.x - startX) / (endX - startX);
                float fValue = (float)minValue + pct * (float)(maxValue - minValue);
                int roundedValue = Mathf.RoundToInt(fValue);
                Value = roundedValue; // will replace the slider knob.

                // SNAP X to closest int
                localProjectedWidgetPosition.x = startX + ((float)roundedValue - minValue) * (endX - startX) / (float)(maxValue - minValue);
                // SNAP Y to middle of knob object. TODO: use actual knob dimensions
                localProjectedWidgetPosition.y = -height + 0.02f;
                // SNAP Z to the thickness of the knob
                localProjectedWidgetPosition.z = -0.005f;

                // Haptic intensity as we go deeper into the widget.
                float intensity = Mathf.Clamp01(0.001f + 0.999f * localWidgetPosition.z / UIElement.collider_min_depth_deep);
                intensity *= intensity; // ease-in

                // TODO : Re-enable
                VRInput.SendHaptic(VRInput.rightController, 0.005f, intensity);
            }

            Vector3 worldProjectedWidgetPosition = transform.TransformPoint(localProjectedWidgetPosition);
            cursorShapeTransform.position = worldProjectedWidgetPosition;
        }

        public static void CreateUITimeBar(
            string sliderName,
            Transform parent,
            Vector3 relativeLocation,
            float width,
            float height,
            float thickness,
            int min_slider_value,
            int max_slider_value,
            int cur_slider_value,
            Material background_material,
            Material knob_material,
            Color background_color,
            Color knob_color,
            string caption)
        {
            GameObject go = new GameObject(sliderName);
            go.tag = "UICollider";

            // Find the anchor of the parent if it is a UIElement
            Vector3 parentAnchor = Vector3.zero;
            if (parent)
            {
                UIElement elem = parent.gameObject.GetComponent<UIElement>();
                if (elem)
                {
                    parentAnchor = elem.Anchor;
                }
            }

            UITimeBar uiTimeBar = go.AddComponent<UITimeBar>(); // NOTE: also creates the MeshFilter, MeshRenderer and Collider components
            uiTimeBar.relativeLocation = relativeLocation;
            uiTimeBar.transform.parent = parent;
            uiTimeBar.transform.localPosition = parentAnchor + relativeLocation;
            uiTimeBar.transform.localRotation = Quaternion.identity;
            uiTimeBar.transform.localScale = Vector3.one;
            uiTimeBar.width = width;
            uiTimeBar.height = height;
            uiTimeBar.thickness = thickness;
            uiTimeBar.minValue = min_slider_value;
            uiTimeBar.maxValue = max_slider_value;
            uiTimeBar.currentValue = cur_slider_value;

            // Setup the Meshfilter
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                // TODO: new mesh, with time ticks texture
                meshFilter.sharedMesh = UIUtils.BuildBoxEx(width, height, thickness);
                uiTimeBar.Anchor = Vector3.zero;
                BoxCollider coll = go.GetComponent<BoxCollider>();
                if (coll != null)
                {
                    Vector3 initColliderCenter = meshFilter.sharedMesh.bounds.center;
                    Vector3 initColliderSize = meshFilter.sharedMesh.bounds.size;
                    if (initColliderSize.z < UIElement.collider_min_depth_shallow)
                    {
                        coll.center = new Vector3(initColliderCenter.x, initColliderCenter.y, UIElement.collider_min_depth_shallow / 2.0f);
                        coll.size = new Vector3(initColliderSize.x, initColliderSize.y, UIElement.collider_min_depth_shallow);
                    }
                    else
                    {
                        coll.center = initColliderCenter;
                        coll.size = initColliderSize;
                    }
                    coll.isTrigger = true;
                }
            }

            // Setup the MeshRenderer
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null && background_material != null)
            {
                // Clone the material.
                meshRenderer.sharedMaterial = Instantiate(background_material);
                uiTimeBar.BaseColor = background_color;

                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.renderingLayerMask = 4; // "LightLayer 3"
            }

            // KNOB
            Vector3 knobPosition = new Vector3(0, 0, 0);
            float newKnobHeadWidth = uiTimeBar.knobHeadWidth;
            float newKnobHeadHeight = uiTimeBar.knobHeadHeight;
            float newKnobHeadDepth = uiTimeBar.knobHeadDepth;
            float newKnobFootWidth = uiTimeBar.knobFootWidth;
            float newKnobFootHeight = uiTimeBar.knobFootHeight;
            float newKnobFootDepth = uiTimeBar.knobFootDepth;

            uiTimeBar.knob = UITimeBarKnob.CreateUITimeBarKnob("Knob", go.transform, knobPosition, 
                newKnobHeadWidth, newKnobHeadHeight, newKnobHeadDepth, newKnobFootWidth, newKnobFootHeight, newKnobFootDepth, 
                knob_material, knob_color);
        }
    }
}