﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace VRtist
{
    public class Selector : ToolBase
    {
        [Header("Selector Parameters")]
        [SerializeField] protected Transform selectorBrush;
        [SerializeField] protected Material selectionMaterial;
        [SerializeField] private float deadZoneDistance = 0.005f;

        float selectorRadius = 0.03f;
        Color selectionColor = new Color(0f, 167f/255f, 1f);

        Dictionary<GameObject, Matrix4x4> initParentMatrix = new Dictionary<GameObject, Matrix4x4>();
        Dictionary<GameObject, Vector3> initPositions = new Dictionary<GameObject, Vector3>();
        Dictionary<GameObject, Quaternion> initRotations = new Dictionary<GameObject, Quaternion>();
        Dictionary<GameObject, Vector3> initScales = new Dictionary<GameObject, Vector3>();
        Vector3 initControllerPosition;
        Quaternion initControllerRotation;

        Matrix4x4 initControllerMatrix;

        public enum SelectorModes { Select = 0, Eraser }
        public SelectorModes mode = SelectorModes.Select;

        const float deadZone = 0.3f;
        const float scaleFactor = 1.1f;

        int groupId = 0;

        void Start()
        {
            Init();
        }

        protected void Init()
        {
            selectorBrush.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", selectionColor);
            selectorBrush.localScale = new Vector3(selectorRadius, selectorRadius, selectorRadius);
            updateButtonsColor();
            Selection.selectionMaterial = selectionMaterial;
            Selection.OnSelectionChanged += OnSelectionChanged;
        }

        protected override void DoUpdate(Vector3 position, Quaternion rotation)
        {
            //if (uiTools.isOverUI()) { return; }

            if (VRInput.GetValue(VRInput.rightController, CommonUsages.grip) <= deadZone)
            {
                // Change selector size
                Vector2 val = VRInput.GetValue(VRInput.rightController, CommonUsages.primary2DAxis);
                if (val != Vector2.zero)
                {
                    if (val.y > deadZone) { selectorRadius *= scaleFactor; }//+= 0.001f; }
                    if (val.y < -deadZone) { selectorRadius /= scaleFactor; }//-= 0.001f; }
                    selectorRadius = Mathf.Clamp(selectorRadius, 0.001f, 0.5f);
                    selectorBrush.localScale = new Vector3(selectorRadius, selectorRadius, selectorRadius);
                }
            }

            switch (mode)
            {
                case SelectorModes.Select: UpdateSelect(position, rotation); break;
                case SelectorModes.Eraser: UpdateEraser(position, rotation); break;
            }
        }

        float scale = 1f;        

        void SetControllerVisible(bool visible)
        {
            transform.localScale = visible ? Vector3.one : Vector3.zero;
        }

        void InitDuplicateMoveTransforms()
        {
            VRInput.GetControllerTransform(VRInput.rightController, out initControllerPosition, out initControllerRotation);
            initControllerMatrix = (transform.parent.localToWorldMatrix * Matrix4x4.TRS(initControllerPosition, initControllerRotation, Vector3.one)).inverse;

            initParentMatrix.Clear();
            initPositions.Clear();
            initRotations.Clear();
            initScales.Clear();
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                initParentMatrix[data.Value] = data.Value.transform.parent.localToWorldMatrix;
                initPositions[data.Value] = data.Value.transform.localPosition;
                initRotations[data.Value] = data.Value.transform.localRotation;
                initScales[data.Value] = data.Value.transform.localScale;
            }
            scale = 1f;            
        }

        bool outOfDeadZone = false;
        protected bool gripped = false;
        protected CommandGroup undoGroup = null;

        protected void OnStartGrip()
        {
            undoGroup = new CommandGroup();

            InitDuplicateMoveTransforms();
            outOfDeadZone = false;
            gripped = true;
        }

        private void ManageMoveObjectsUndo()
        {
            List<GameObject> objects = new List<GameObject>();
            List<Vector3> beginPositions = new List<Vector3>();
            List<Quaternion> beginRotations = new List<Quaternion>();
            List<Vector3> beginScales = new List<Vector3>();
            List<Vector3> endPositions = new List<Vector3>();
            List<Quaternion> endRotations = new List<Quaternion>();
            List<Vector3> endScales = new List<Vector3>();

            int i = 0;
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                GameObject gObject = data.Value;
                objects.Add(gObject);
                beginPositions.Add(initPositions[gObject]);
                beginRotations.Add(initRotations[gObject]);
                beginScales.Add(initScales[gObject]);

                endPositions.Add(gObject.transform.localPosition);
                endRotations.Add(gObject.transform.localRotation);
                endScales.Add(gObject.transform.localScale);
            }

            new CommandMoveObjects(objects, beginPositions, beginRotations, beginScales, endPositions, endRotations, endScales).Submit();

        }
        protected void OnEndGrip()
        {
            ManageMoveObjectsUndo();
            undoGroup.Submit();
            undoGroup = null;

            gripped = false;
        }

        private void OnSelectionChanged(object sender, SelectionChangedArgs args)
        {
            InitDuplicateMoveTransforms();
            outOfDeadZone = false;            
        }
        private void UpdateSelect(Vector3 position, Quaternion rotation)
        {
            // Move & Duplicate selection
            bool buttonAJustPressed = false;

            // get rightPrimaryState
            VRInput.ButtonEvent(VRInput.rightController, CommonUsages.primaryButton, 
                () =>
                {
                    buttonAJustPressed = true;
                });

            // get rightPrimaryState
            VRInput.ButtonEvent(VRInput.rightController, CommonUsages.secondaryButton,
                () =>
                {
                    var undoGroup = new CommandGroup();
                    List<GameObject> selectedObjects = new List<GameObject>();
                    foreach (KeyValuePair<int, GameObject> data in Selection.selection)
                    {
                        selectedObjects.Add(data.Value);
                    }
                    ClearSelection();
                    foreach(var gobject in selectedObjects)
                    {
                        new CommandRemoveGameObject(gobject).Submit();
                        gobject.transform.parent = Utils.GetOrCreateTrash().transform;
                    }
                    undoGroup.Submit();
                });

            VRInput.ButtonEvent(VRInput.rightController, CommonUsages.grip, OnStartGrip, OnEndGrip);

            SetControllerVisible(!gripped || Selection.selection.Count == 0);

            if (gripped)
            {
                // move selection
                if (buttonAJustPressed)
                {
                    DuplicateSelection();
                    InitDuplicateMoveTransforms();
                    outOfDeadZone = true;
                }

                Vector3 p;
                Quaternion r;
                VRInput.GetControllerTransform(VRInput.rightController, out p, out r);

                if (!outOfDeadZone && Vector3.Distance(p, initControllerPosition) > deadZoneDistance)
                    outOfDeadZone = true;

                if (!outOfDeadZone)
                    return;

                Transform parent = transform.parent;

                Vector2 joystickAxis = VRInput.GetValue(VRInput.rightController, CommonUsages.primary2DAxis);
                if (joystickAxis.y > deadZone)
                    scale *= scaleFactor;
                if (joystickAxis.y < -deadZone)
                    scale /= scaleFactor;


                Matrix4x4 controllerMatrix = parent.localToWorldMatrix * Matrix4x4.TRS(p, r, new Vector3(scale, scale, scale)) * initControllerMatrix;

                foreach (KeyValuePair<int, GameObject> data in Selection.selection)
                {
                    var meshParentTransform = data.Value.transform.parent;
                    Matrix4x4 meshParentMatrixInverse = new Matrix4x4();
                    if (meshParentTransform)
                        meshParentMatrixInverse = meshParentTransform.worldToLocalMatrix;
                    else
                        meshParentMatrixInverse = Matrix4x4.identity;
                    Matrix4x4 transformed = meshParentMatrixInverse * controllerMatrix * initParentMatrix[data.Value] * Matrix4x4.TRS(initPositions[data.Value], initRotations[data.Value], initScales[data.Value]);
                    data.Value.transform.localPosition = new Vector3(transformed.GetColumn(3).x, transformed.GetColumn(3).y, transformed.GetColumn(3).z);
                    data.Value.transform.localRotation = transformed.rotation;
                    data.Value.transform.localScale = transformed.lossyScale;
                }
            }
        }

        private void UpdateEraser(Vector3 position, Quaternion rotation)
        {
            // Nothing for now
        }

        private List<GameObject> GetGroupSiblings(GameObject gObject)
        {
            List<GameObject> objects = new List<GameObject>();
            Transform parent = gObject.transform.parent;
            if (parent.name.StartsWith("Group__"))
            {
                for (int i = 0; i < parent.childCount; i++)
                    objects.Add(parent.GetChild(i).gameObject);
            }
            else
            {
                objects.Add(gObject);
            }
            return objects;
        }

        public bool AddToSelection(GameObject gObject)
        {
            return Selection.AddToSelection(gObject);
        }

        public void AddSiblingsToSelection(GameObject gObject, bool haptic = true)
        {
            bool somethingWasAddedToSelection = false;
            List<GameObject> objects = GetGroupSiblings(gObject);
            foreach (GameObject gobj in objects)
            {
                if (AddToSelection(gobj))
                    somethingWasAddedToSelection = true;
            }

            if (haptic && somethingWasAddedToSelection)
            {
                VRInput.rightController.SendHapticImpulse(0, 1, 0.1f);
            }

            new CommandAddToSelection(objects).Submit();
        }
        public bool RemoveFromSelection(GameObject gObject)
        {
            return Selection.RemoveFromSelection(gObject);
        }
        public void RemoveSiblingsFromSelection(GameObject gObject, bool haptic = true)
        {
            bool somethingWasRemovedFromSelection = false;
            List<GameObject> objects = GetGroupSiblings(gObject);
            foreach (GameObject gobj in objects)
            {
                if (RemoveFromSelection(gobj))
                    somethingWasRemovedFromSelection = true;
            }

            if (haptic && somethingWasRemovedFromSelection)
            {
                VRInput.rightController.SendHapticImpulse(0, 1, 0.1f);
            }

            new CommandRemoveFromSelection(objects).Submit();
        }

        public void ClearSelection()
        {
            List<GameObject> objects = new List<GameObject>();
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                objects.Add(data.Value);
            }
            new CommandRemoveFromSelection(objects).Submit();

            Selection.ClearSelection();           
        }

        public void DuplicateSelection()
        {
            ManageMoveObjectsUndo();

            List<GameObject> clones = new List<GameObject>();

            Dictionary<Transform, Transform> groups = new Dictionary<Transform, Transform>();

            GameObject[] selectedObjects = new GameObject[Selection.selection.Count];
            int i = 0;
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                selectedObjects[i] = data.Value;
                ++i;
            }

            ClearSelection();

            for(i = 0; i < selectedObjects.Length; i++)
            {
                Transform parent = selectedObjects[i].transform.parent;
                if (parent.name.StartsWith("Group__"))
                {
                    if (!groups.ContainsKey(parent))
                    {
                        GameObject newGroup = new GameObject("Group__" + groupId.ToString());
                        groupId++;
                        groups[parent] = newGroup.transform;
                        newGroup.transform.parent = parent.parent;
                        newGroup.transform.localPosition = parent.localPosition;
                        newGroup.transform.localRotation = parent.localRotation;
                        newGroup.transform.localScale = parent.localScale;
                    }

                    GameObject clone = Utils.CreateInstance(selectedObjects[i], groups[parent]);
                    clones.Add(clone);

                    new CommandDuplicateGameObject(clone, selectedObjects[i]).Submit();
                }
                else
                {
                    GameObject clone = Utils.CreateInstance(selectedObjects[i], selectedObjects[i].transform.parent);
                    clones.Add(clone);

                    new CommandDuplicateGameObject(clone, selectedObjects[i]).Submit();
                }
            }

            foreach (GameObject clone in clones)
            {
                AddToSelection(clone);
            }
        }

        public void OnSelectMode()
        {
            mode = SelectorModes.Select;
            selectorBrush.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", selectionColor);
            updateButtonsColor();
        }

        public void OnEraserMode()
        {
            mode = SelectorModes.Eraser;
            selectorBrush.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", Color.red);
            updateButtonsColor();
        }

        void updateButtonsColor()
        {
            if (!panel)
                return;

            // NOTE: currently the SelectorPanel has 4 children of type UIButton
            //       which have on Canvas children, with in turn has one Image and one Text children.
            for (int i = 0; i < panel.childCount; i++)
            {
                GameObject child = panel.GetChild(i).gameObject;
                Canvas canvas = child.GetComponentInChildren<Canvas>();
                if (canvas != null)
                {
                    Image image = canvas.gameObject.GetComponentInChildren<Image>();
                    if (image != null)
                    {
                        image.color = Selection.UnselectedColor;
                        if (child.name == "Select" && mode == SelectorModes.Select)
                            image.color = Selection.SelectedColor;
                        if (child.name == "Eraser" && mode == SelectorModes.Eraser)
                            image.color = Selection.SelectedColor;
                    }
                }
            }
        }

        public void OnLinkAction()
        {
            if (Selection.selection.Count <= 1)
                return;

            GameObject container = new GameObject("Group__" + groupId.ToString());
            groupId++;

            SortedSet<Transform> groups = new SortedSet<Transform>();

            bool groupHasParent = false;
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                GameObject gobject = data.Value;
                Transform parent = gobject.transform.parent;
                if (parent && parent.name.StartsWith("Group__"))
                {
                    if (!groupHasParent)
                    {
                        container.transform.parent = parent.parent;
                        groupHasParent = true;
                    }

                    // reparent
                    groups.Add(gobject.transform.parent);
                    gobject.transform.parent = container.transform;
                }
                else
                {
                    if (!groupHasParent)
                    {
                        container.transform.parent = data.Value.transform.parent;
                        groupHasParent = true;
                    }
                    data.Value.transform.parent = container.transform;
                    
                }
            }

            foreach (Transform group in groups)
            {
                if (group.childCount == 0)
                    Destroy(group.gameObject);
            }
        }

        public void OnUnlinkAction()
        {
            SortedSet<Transform> groups = new SortedSet<Transform>();
            foreach (KeyValuePair<int, GameObject> data in Selection.selection)
            {
                GameObject gobject = data.Value;
                Transform parent = gobject.transform.parent;
                if (parent && parent.name.StartsWith("Group__"))
                {
                    groups.Add(gobject.transform.parent);
                }
            }

            foreach (Transform group in groups)
            {
                for (int i = group.childCount - 1; i >= 0; i--)
                {
                    group.GetChild(i).parent = group.parent;
                }
                Destroy(group.gameObject);
            }

        }
    }
}