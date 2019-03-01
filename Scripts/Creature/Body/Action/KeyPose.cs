﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using SprCs;
using SprUnity;

#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(KeyPose))]
public class KeyPoseEditor : Editor {

    void OnEnable() {
        SceneView.onSceneGUIDelegate += OnSceneGUI;
    }

    void OnDisable() {
        SceneView.onSceneGUIDelegate -= OnSceneGUI;
    }

    // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----

    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        KeyPose keyPose = (KeyPose)target;

        if (GUILayout.Button("Test")) {
            keyPose.Action();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Use Current Pose")) {
            keyPose.InitializeByCurrentPose();
        }
    }

    public void OnSceneGUI(SceneView sceneView) {
        KeyPose keyPose = (KeyPose)target;

        var body = GameObject.FindObjectOfType<Body>();

        foreach (var boneKeyPose in keyPose.boneKeyPoses) {
            if (boneKeyPose.usePosition) {
                EditorGUI.BeginChangeCheck();
                if (boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.World) {
                    Vector3 position = Handles.PositionHandle(boneKeyPose.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(target, "Change KeyPose Target Position");
                        boneKeyPose.position = position;
                        if (body) {
                            boneKeyPose.ConvertWorldToLocal(body);
                        }
                        EditorUtility.SetDirty(target);
                    }
                }
                if (boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.BoneBaseLocal && body != null) {
                    boneKeyPose.ConvertLocalToWorld(body);
                    Vector3 position = Handles.PositionHandle(boneKeyPose.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(target, "Change KeyPose Target Position");
                        boneKeyPose.position = position;
                        boneKeyPose.ConvertWorldToLocal(body);
                        EditorUtility.SetDirty(target);
                    }
                }
            }

            if (boneKeyPose.useRotation) {
                EditorGUI.BeginChangeCheck();
                if (boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.World) {
                    Quaternion rotation = Handles.RotationHandle(boneKeyPose.rotation, boneKeyPose.position);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(target, "Change KeyPose Target Rotation");
                        boneKeyPose.rotation = rotation;
                        if (body) {
                            boneKeyPose.ConvertWorldToLocal(body);
                        }
                        EditorUtility.SetDirty(target);
                    }
                }
                if (boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.BoneBaseLocal && body != null) {
                    boneKeyPose.ConvertLocalToWorld(body);
                    Quaternion rotation = Handles.RotationHandle(boneKeyPose.rotation, boneKeyPose.position);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(target, "Change KeyPose Target Position");
                        boneKeyPose.rotation = rotation;
                        boneKeyPose.ConvertWorldToLocal(body);
                        EditorUtility.SetDirty(target);
                    }
                }
            }
        }
    }
}
/*
[CustomPropertyDrawer(typeof(BoneKeyPose))]
public class BoneKeyPoseDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        base.OnGUI(position, property, label);
    }
}
*/
#endif

[Serializable]
public class BoneKeyPose {
    public string label {
        get {
            return boneId.ToString();
        }
    }
    public HumanBodyBones boneId = HumanBodyBones.Hips;
    public Vector3 position = new Vector3();
    public Quaternion rotation = new Quaternion();
    // 以下はローカル座標とするためのものだが、ここに保存するには長いか？
    public enum CoordinateMode {
        World, // Parentのローカル座標系でのある姿勢
        BoneBaseLocal, // あるオブジェクトの方向を向く
    };
    public CoordinateMode coordinateMode;
    public HumanBodyBones coordinateParent;
    public Vector3 localPosition = new Vector3();
    public Vector3 normalizedLocalPosition = new Vector3();
    public Quaternion localRotation = Quaternion.identity;
    public bool usePosition = true;
    public bool useRotation = true;

    public void ConvertLocalToWorld(Body body = null) {
        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body != null) {
            Bone coordinateBaseBone = body[coordinateParent];
            position = coordinateBaseBone.transform.position + coordinateBaseBone.transform.rotation * localPosition;
            rotation = coordinateBaseBone.transform.rotation * localRotation;
        }
    }
    public void ConvertWorldToLocal(Body body = null) {
        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body != null) {
            Bone coordinateBaseBone = body[coordinateParent];
            localPosition = Quaternion.Inverse(coordinateBaseBone.transform.rotation) * (position - coordinateBaseBone.transform.position);
            normalizedLocalPosition = localPosition / body.height;
            localRotation = Quaternion.Inverse(coordinateBaseBone.transform.rotation) * rotation;
        }
    }
    public void ConvertLocalToLocal(Body body = null) {
        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body != null) {
        }
    }
}

#if UNITY_EDITOR
[CreateAssetMenu(menuName = "Action/Create KeyPose")]
#endif
public class KeyPose : ScriptableObject {
    public List<BoneKeyPose> boneKeyPoses = new List<BoneKeyPose>();

    public float testDuration = 1.0f;
    public float testSpring = 1.0f;
    public float testDamper = 1.0f;

    public void InitializeByCurrentPose(Body body = null) {
        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body != null) {
            boneKeyPoses.Clear();
            foreach (var bone in body.bones) {
                if (bone.ikEndEffector != null && bone.controller != null && bone.controller.enabled) {
                    BoneKeyPose boneKeyPose = new BoneKeyPose();
                    boneKeyPose.position = bone.transform.position;
                    boneKeyPose.rotation = bone.transform.rotation;

                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) {
                        if (((HumanBodyBones)i).ToString() == bone.label) {
                            boneKeyPose.boneId = (HumanBodyBones)i;
                        }
                    }

                    if (bone.ikEndEffector.phIKEndEffector != null) {

                        if (bone.ikEndEffector.phIKEndEffector.IsPositionControlEnabled()) {
                            boneKeyPose.position = bone.ikEndEffector.phIKEndEffector.GetTargetPosition().ToVector3();
                            boneKeyPose.usePosition = true;
                        } else {
                            boneKeyPose.usePosition = false;
                        }

                        if (bone.ikEndEffector.phIKEndEffector.IsOrientationControlEnabled()) {
                            boneKeyPose.rotation = bone.ikEndEffector.phIKEndEffector.GetTargetOrientation().ToQuaternion();
                            boneKeyPose.useRotation = true;
                        } else {
                            boneKeyPose.useRotation = false;
                        }

                    } else {

                        if (bone.ikEndEffector.desc.bPosition) {
                            boneKeyPose.position = ((Vec3d)(bone.ikEndEffector.desc.targetPosition)).ToVector3();
                            boneKeyPose.usePosition = true;
                        } else {
                            boneKeyPose.usePosition = false;
                        }

                        if (bone.ikEndEffector.desc.bOrientation) {
                            boneKeyPose.rotation = ((Quaterniond)(bone.ikEndEffector.desc.targetOrientation)).ToQuaternion();
                            boneKeyPose.useRotation = true;
                        } else {
                            boneKeyPose.useRotation = false;
                        }

                    }

                    boneKeyPose.ConvertWorldToLocal();
                    boneKeyPoses.Add(boneKeyPose);
                }
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
#endif
        }
    }

    public void Action(Body body = null, float duration = -1, float startTime = -1, float spring = -1, float damper = -1) {
        if (duration < 0) { duration = testDuration; }
        if (startTime < 0) { startTime = 0; }
        if (spring < 0) { spring = testSpring; }
        if (damper < 0) { damper = testDamper; }

        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body != null) {
            foreach (var boneKeyPose in boneKeyPoses) {
                if (boneKeyPose.usePosition || boneKeyPose.useRotation) {
                    Bone bone = body[boneKeyPose.boneId];
                    var pose = new Pose(boneKeyPose.position, boneKeyPose.rotation);
                    if(boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.BoneBaseLocal) {
                        Bone baseBone = body[boneKeyPose.coordinateParent];
                        pose.position = baseBone.transform.position + baseBone.transform.rotation * boneKeyPose.localPosition;
                        pose.rotation = boneKeyPose.localRotation * baseBone.transform.rotation;
                    }
                    var springDamper = new Vector2(spring, damper);
                    bone.controller.AddSubMovement(pose, springDamper, startTime + duration, duration, usePos: boneKeyPose.usePosition, useRot: boneKeyPose.useRotation);
                }
            }
        }
    }

    public List<BoneKeyPose> GetBoneKeyPoses(Body body) {
        if (body == null) { body = GameObject.FindObjectOfType<Body>(); }
        if (body == null) { return null; }
        List<BoneKeyPose> boneKeyPoses = new List<BoneKeyPose>();
        foreach (var boneKeyPose in boneKeyPoses) {
            BoneKeyPose keyPoseApplied = new BoneKeyPose();
            Bone coordinateBaseBone = body[boneKeyPose.coordinateParent];
            Bone controlBone = body[boneKeyPose.boneId];
            //Vector3 targetDir = target ? target.transform.position - coordinateBaseBone.transform.position : coordinateBaseBone.transform.rotation * Vector3.forward;

            keyPoseApplied.boneId = boneKeyPose.boneId;
            if (boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.BoneBaseLocal) {
                // 位置の補正
                //Vector3 pos = coordinateBaseBone.transform.position + Quaternion.LookRotation(targetDir, coordinateBaseBone.transform.rotation * Vector3.up) * boneKeyPose.localPosition;
                keyPoseApplied.position = coordinateBaseBone.transform.position + coordinateBaseBone.transform.rotation * boneKeyPose.localPosition;
                // 姿勢の補正
                //Quaternion rot = boneKeyPose.localRotation * Quaternion.LookRotation(targetDir, coordinateBaseBone.transform.rotation * Vector3.up);
                keyPoseApplied.rotation = boneKeyPose.localRotation * coordinateBaseBone.transform.rotation;
            }else if(boneKeyPose.coordinateMode == BoneKeyPose.CoordinateMode.World) {
                keyPoseApplied.position = boneKeyPose.position;
                keyPoseApplied.rotation = boneKeyPose.rotation;
            }
            keyPoseApplied.usePosition = boneKeyPose.usePosition;
            keyPoseApplied.useRotation = boneKeyPose.useRotation;

            boneKeyPoses.Add(boneKeyPose);
        }
        return boneKeyPoses;
    }
}