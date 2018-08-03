﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace InteraWare {

    public class AttentionPerception : Person.Perception {
        public float attention = 0.0f;
        public float attentionByDistance = 0.0f;
        public float attentionByDistanceDecrease = 0.0f;

        public float lastDistance = 0.0f;

        public override void OnDrawGizmos(Person person) {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(person.transform.position, 0.3f * 1.0f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(person.transform.position, 0.3f * attention);
        }
    }

    public class Attention : MonoBehaviour {

        public Body body = null;
        public LookController lookController = null;

        // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
        // Parameters

        // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
        // Internal Variables

        // Gaze Transition Timer (unit: second)
        private float timeFromGazeTransition = 0.0f;
        private float timeUntilGazeTransition = 0.0f;
        private float nextGazeTransitionTime = 0.0f;

        [HideInInspector]
        public Person currentAttentionTarget = null;

        // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
        // MonoBehaviour Methods

        void Start() {
        }

        void FixedUpdate() {
            if (currentAttentionTarget == null) {
                if (Person.persons.Count > 0) {
                    currentAttentionTarget = Person.persons[0];
                } else {
                    return;
                }
            }

            CompAttention();
            GazeTransition();
        }

        // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
        // Public APIs

		public void OverrideGazeTarget(Person person, float attention = -1, bool forceStraight = false) {
            if (attention >= 0.0f) {
                person.AddPerception<AttentionPerception>().attention = attention;
            }
			ChangeGazeTarget(person, forceStraight);
        }

        public void OverrideGazeTransitionTime(float time) {
            timeFromGazeTransition = 0.0f;
            timeUntilGazeTransition = time;
            nextGazeTransitionTime = time;
        }

        // ----- ----- ----- ----- ----- ----- ----- ----- ----- -----
        // Private Methods

        void CompAttention() {
            foreach (var person in Person.persons) {
                var attentionInfo = person.AddPerception<AttentionPerception>();

                if (person.human) {
                    // 距離による注意
                    var pos = person.transform.position; pos.y = 0;
                    float distance = pos.magnitude;
                    float min = 0.5f, max = 2.0f; // [m]
                    float baseAttention = 0.07f;
                    attentionInfo.attentionByDistance = (1 - (Mathf.Clamp(distance, min, max) - min) / (max - min)) * (1.0f - baseAttention) + baseAttention;

                    // 距離の減少による注意
                    float distanceDecreaseVel = (attentionInfo.lastDistance - distance) / Time.fixedDeltaTime;
                    if (distanceDecreaseVel > 0) {
                        float maxVel = 10.0f; // [m/sec]
                        float attentionByDistanceDecrease = Mathf.Clamp01(distanceDecreaseVel / maxVel);
                        attentionInfo.attentionByDistanceDecrease = Mathf.Max(attentionInfo.attentionByDistanceDecrease, attentionByDistanceDecrease);
                    } else {
                        float becomeZeroTime = 3.0f; // [sec]
                        attentionInfo.attentionByDistanceDecrease -= (Time.fixedDeltaTime / becomeZeroTime);
                        attentionInfo.attentionByDistanceDecrease = Mathf.Clamp01(attentionInfo.attentionByDistanceDecrease);
                    }
                    attentionInfo.lastDistance = distance;

                    // 注意量
                    // attentionInfo.attention = Mathf.Max(attentionInfo.attentionByDistance, attentionInfo.attentionByDistanceDecrease);

                } else {
                    // 背景オブジェクトには一律の注意量を与える
                    // attentionInfo.attention = 0.2f;
                }
            }
        }

        void GazeTransition() {
            // Gaze Transition Timer
            timeFromGazeTransition += Time.fixedDeltaTime;

            // ----- ----- ----- ----- -----

            if (lookController.inAction) {
                return; // 頭を動かしている間や直後は次の視線移動をしない 
            }

            // ----- ----- ----- ----- -----

            Person newAttentionTarget = null;
            Vector3 headPos = body["Head"].transform.position;
            Vector3 currDir;
            if (currentAttentionTarget != null) {
                currDir = (currentAttentionTarget.transform.position - headPos);
            } else {
                currDir = new Vector3(0, 0, 1);
            }

            // ----- ----- ----- ----- -----

            if (nextGazeTransitionTime < timeFromGazeTransition) {
                // Determine Gaze Target according to Attention Value
                List<Person> candidates = new List<Person>();
                List<float> probs = new List<float>();

                foreach (Person person in Person.persons) {
                    if (person != currentAttentionTarget) {
                        // 位置のおかしな対象はスキップする
                        if (person.transform.position.z < 0.3f || person.transform.position.y > 2.0f) { continue; }

                        // 現在の注視対象とのなす角を求める
                        Vector3 candDir = (person.transform.position - headPos);
                        float angleDistance = Vector3.Angle(currDir, candDir);

                        // 角度に従って遷移確率を求める（角度が小さいほど高確率で遷移する）
                        float prob = Mathf.Max(0, (1.0f * Mathf.Exp(-angleDistance / 10.0f)));

                        // 注視対象候補としてリストに追加
                        candidates.Add(person);
                        probs.Add(prob);
                    }
                }

                // 正規化
                {
                    float totalProb = probs.Sum();
                    if (totalProb != 0) {
                        for (int i = 0; i < probs.Count; i++) { probs[i] /= totalProb; }
                    } else {
                        totalProb = probs.Sum();
                        for (int i = 0; i < probs.Count; i++) { probs[i] = probs[i] / totalProb; }
                    }
                }

                // TDAに基づいて遷移確率にバイアスをかける
                for (int i = 0; i < probs.Count; i++) {
                    // 現在の注視対象よりTDAの大きな対象に（TDA差に比例して）遷移しやすくする
                    float diffAttention = candidates[i].AddPerception<AttentionPerception>().attention - currentAttentionTarget.AddPerception<AttentionPerception>().attention;
                    if (diffAttention > 0) {
                        probs[i] += diffAttention * 20;
                    }
                }

                // 再度正規化
                {
                    float totalProb = probs.Sum();
                    if (totalProb != 0) {
                        for (int i = 0; i < probs.Count; i++) {
                            probs[i] /= totalProb;
                        }
                    } else {
                        totalProb = probs.Sum();
                        for (int i = 0; i < probs.Count; i++) { probs[i] = probs[i] / totalProb; }
                    }
                }

                // 選択
                float r = Random.value;
                float accumProb = 0.0f;
                for (int i = 0; i < probs.Count; i++) {
                    accumProb += probs[i];
                    if (r < accumProb) {
                        newAttentionTarget = candidates[i];
                        break;
                    }
                }

            }

            // ----- ----- ----- ----- -----

            ChangeGazeTarget(newAttentionTarget);

        }

		void ChangeGazeTarget(Person newAttentionTarget, bool forceStraight = false) {
            if (newAttentionTarget != null) {
                // 目を動かす
                var attention = newAttentionTarget.AddPerception<AttentionPerception>().attention;
                lookController.target = newAttentionTarget.gameObject;
                lookController.speed = 1.0f;
                lookController.stare = attention;
				if (forceStraight || (newAttentionTarget.human && lookController.straight == false)) {
                    lookController.straight = true;
                } else {
                    lookController.straight = false;
                }

                // 次に視線移動するまでの時間を決定する
                float x_ = LinearFunction(new Vector2(0, 100), new Vector2(1, 150), newAttentionTarget.AddPerception<AttentionPerception>().attention);
                float y_ = LinearFunction(new Vector2(0, 79), new Vector2(1, 32), newAttentionTarget.AddPerception<AttentionPerception>().attention);

                float b = y_;
                float a = -(y_ / x_);

                float x = 15, y = 0;
                for (int i = 0; i < 100; i++) { // 棄却法で指定分布に従う乱数を生成
                    x = Random.value * x_;
                    y = Random.value * y_;
                    if (y < a * x + b) {
                        break;
                    }
                }
                nextGazeTransitionTime = x / 30.0f + newAttentionTarget.AddPerception<AttentionPerception>().attention * 0.5f;
                timeFromGazeTransition = 0.0f;

                currentAttentionTarget = newAttentionTarget;
            }
        }

        float Clip(float min, float max, float value) {
            return Mathf.Min(Mathf.Max(min, value), max);
        }

        float LinearFunction(Vector2 p1, Vector2 p2, float x) {
            float a = (p2.y - p1.y) / (p2.x - p1.x);
            float b = p1.y - p1.x * a;
            return a * x + b;
        }
    }

}