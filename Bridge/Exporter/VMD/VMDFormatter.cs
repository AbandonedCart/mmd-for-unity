using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MMD.VMD;

namespace MMD
{
    namespace VMD
    {
        public class VMDFormatter
        {
            GameObject mmd_object;
            VMDFormat format;
            MMDEngine engine;   // スケールを取得するために必要

            public VMDFormat Format { get { return format; } }

            public VMDFormatter(GameObject target)
            {
                mmd_object = target;
                engine = mmd_object.GetComponent<MMDEngine>();
                format = new VMDFormat();
                format.header = new VMDFormat.Header();
                format.header.vmd_model_name = target.name;
                format.header.vmd_header = "Vocaloid Motion Data 0002";
                format.motion_list = new VMDFormat.MotionList();
                format.skin_list = new VMDFormat.SkinList();
                format.camera_list = new VMDFormat.CameraList();
                format.light_list = new VMDFormat.LightList();
                format.self_shadow_list = new VMDFormat.SelfShadowList();
            }

            public VMDFormat InsertMorph(uint insert_frame_no)
            {
                // Expression以下のGameObjectを取り出し
                var expression = mmd_object.transform.FindChild("Expression");
                var expressions = new List<Transform>();
                for (int i = 0; i < expression.childCount; i++)
                    expressions.Add(expression.GetChild(i));

                // GameObjectごとにVMDFormatを構成
                foreach (var exp in expressions)
                {
                    if (exp.name == "base" || exp.localPosition.z == 0f) continue;
                    var skin = new VMDFormat.SkinData();
                    skin.frame_no = insert_frame_no;
                    skin.skin_name = exp.name;
                    skin.weight = exp.localPosition.z;

                    format.skin_list.Insert(skin);
                }
                return format;
            }

            class VQSet
            {
                public Vector3 v = Vector3.zero;
                public Quaternion q = Quaternion.identity;
            }

            VQSet RecursiveCancelAddition(BoneController ctrl)
            {
                VQSet vq;

                var parent = ctrl.additive_parent;
                if (parent != null)
                {
                    var t = ctrl.transform;
                    vq = RecursiveCancelAddition(parent);
                    vq.v -= t.localPosition;
                    vq.q *= Quaternion.Inverse(t.localRotation);
                    return vq;
                }

                return new VQSet(); // ここまで到達すると恐らく親っぽい感じがする
            }

            VQSet CancelAddition(Transform bone)
            {
                var ctrl = bone.GetComponent<BoneController>();
                var vq = RecursiveCancelAddition(ctrl);
                vq.v = bone.localPosition + vq.v;
                vq.q = bone.localRotation * vq.q;
                return vq;
            }

            public VMDFormat InsertPose(uint insert_frame_no)
            {
                var root = mmd_object.transform.FindChild("Model");
                var bone_list = GetOperatableBones(root);

                foreach (var bone in bone_list)
                {
                    if (bone.localRotation == Quaternion.identity)
                        continue;
                    var motion = new VMDFormat.Motion();
                    motion.frame_no = insert_frame_no;
                    motion.bone_name = bone.name;

                    CancelAddition(bone);

                    motion.location = bone.position * (1f / engine.scale);
                    motion.rotation = bone.localRotation;
                    // interpolationは自動的に初期化されるので無視する

                    format.motion_list.Insert(motion);
                }

                return format;
            }

            void RecursiveAllForBones(List<Transform> bone_list, Transform parent_bone)
            {
                // 再帰的に操作可能なボーンを探索
                for (int i = 0; i < parent_bone.childCount; i++)
                {
                    var child = parent_bone.GetChild(i);
                    var ctrler = child.GetComponent<BoneController>();      // あまりGetComponentしたくない感じ
                    if (ctrler != null)
                    {
                        if (ctrler.operatable && (ctrler.rotatable || ctrler.movable))
                        {
                            bone_list.Add(child);
                        }
                    }
                    RecursiveAllForBones(bone_list, child);
                }
            }

            List<Transform> GetOperatableBones(Transform root)
            {
                var result = new List<Transform>();
                RecursiveAllForBones(result, root);
                return result;
            }
        }
    }
}