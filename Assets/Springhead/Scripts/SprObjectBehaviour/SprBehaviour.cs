﻿using UnityEngine;
using System.Collections;
using System.IO;
using SprCs;

public class SprBehaviourBase : MonoBehaviour {
    // -- DLLパスをセットするWindows API
    [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
    protected static extern bool SetDllDirectory(string lpPathName);

    // -- 一度だけDLLパスをセットする（一度呼べば十分なので）
    public static bool dllPathAlreadySet = false;
    protected void SetDLLPath() {
        if (!dllPathAlreadySet) {
            // 非実行中にはApplication.dataPathは使えないので
            string currDir = Directory.GetCurrentDirectory();

            if (Directory.Exists(currDir + "/Assets/Springhead/Plugins")) {
                // Editor内、あるいはEditorから実行している時
                SetDllDirectory(currDir + "/Assets/Springhead/Plugins");

            } else if (Directory.Exists(currDir + "/Springhead/Plugins")) {
                // ビルドされたものを実行している時
                SetDllDirectory(currDir + "/Springhead/Plugins");

            }
        }
    }

    // -- Spr関連オブジェクトがコンストラクトされる時に自動で呼ぶ
    public SprBehaviourBase() {
        SetDLLPath();
    }
}

public abstract class SprBehaviour : SprBehaviourBase {
    // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
    // 対応するSpringheadオブジェクト

    private ObjectIf sprObject_ = null;
    public ObjectIf sprObject { get { return sprObject_; }  protected set { sprObject_ = value; } }

    // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
    // 派生クラスで実装するメソッド

    // -- DescStructオブジェクトを再構築する
    public abstract void ResetDescStruct();
    // -- DescStructオブジェクトを取得する
    public abstract CsObject GetDescStruct();
    // -- DescオブジェクトをNewして返す
    public abstract CsObject CreateDesc();
    // -- DescStructをDescに適用する
    public abstract void ApplyDesc(CsObject from, CsObject to);
    // -- Sprオブジェクトの構築を行う
    public abstract ObjectIf Build();
    // -- 全てのBuildが完了した後に行う処理を書く。オブジェクト同士をリンクするなど。無くても良い
    public virtual void Link() { }

    // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
    // MonoBehaviourのメソッド

    // --
    public void Reset() {
        ResetDescStruct();
    }

    // --
    private bool awakeCalled = false;
    public void Awake() {
        if (!awakeCalled && GetDescStruct() != null) {
            if (!enabled) { return; }
            sprObject = Build();
            awakeCalled = true;
        }
    }

    // --
    private bool startCalled = false;
    public void Start() {
        if (!startCalled && GetDescStruct() != null) {
            Link();
            // オブジェクトの作成が一通り完了したら一度OnValidateを読んで設定を確実に反映しておく
            OnValidate();
            startCalled = true;
        }
    }

    // --
    public virtual void OnValidate() {
        if (GetDescStruct() == null) {
            ResetDescStruct();
        }

        if (sprObject != null) {
            CsObject d = CreateDesc();
            if (d != null) {
                sprObject.GetDesc(d);
                ApplyDesc(GetDescStruct(), d);
                sprObject.SetDesc(d);
            }
        }
    }
}

public abstract class SprSceneObjBehaviour : SprBehaviour {
    //
    public PHSceneBehaviour phSceneBehaviour {
        get {
            PHSceneBehaviour pb = gameObject.GetComponentInParent<PHSceneBehaviour>();
            if (pb == null) {
                pb = FindObjectOfType<PHSceneBehaviour>();
                if (pb == null) {
                    throw new ObjectNotFoundException("PHSceneBehaviour was not found", gameObject);
                }
            }
            return pb;
        }
    }

    public PHSceneIf phScene {
        get {
            return phSceneBehaviour.sprObject as PHSceneIf;
        }
    }

    //
    public PHSdkIf phSdk {
        get {
            return phScene.GetSdk();
        }
    }
}

public class ObjectNotFoundException : System.Exception {
    public GameObject gameObject { get; private set; }

    public ObjectNotFoundException(string message, GameObject obj) : base(message) {
        gameObject = obj;
    }

    public override string ToString() {
        return gameObject.ToString() + " : " + Message;
    }
}