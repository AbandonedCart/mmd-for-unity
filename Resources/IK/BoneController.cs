using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BoneController : MonoBehaviour
{
	public BoneController additive_parent;
	public float additive_rate;
	public CCDIKSolver ik_solver;
	public BoneController[] ik_solver_targets;

	public bool add_local;
	public bool add_move;
	public bool add_rotate;

    public bool display_flag;
    public bool movable;
    public bool rotatable;
    public bool operatable;

    public bool fixed_axis_flag = false;// 軸制限
    public Vector3 axis_vector;

    public bool enable_local = false;   // ローカル軸
    public Vector3 local_x;
    public Vector3 local_z;

    public string frame_display_name;   // 枠名

	/// <summary>
	/// 簡略化トランスフォーム
	/// </summary>
	[System.Serializable]
	public struct LiteTransform {
		public Vector3 position;	// 位置
		public Quaternion rotation;	// 回転
		
		public LiteTransform(Vector3 p, Quaternion r) {position = p; rotation = r;}
	}
    LiteTransform prev_global_;
    LiteTransform prev_parent_global_;
    Transform additive_parent_transform;

	/// <summary>
	/// 初回更新前処理
	/// </summary>
	void Start()
	{
		if (null != ik_solver) {
			ik_solver = transform.GetComponent<CCDIKSolver>();
			if (0 == ik_solver_targets.Length) {
				ik_solver_targets = Enumerable.Repeat(ik_solver.target, 1)
												.Concat(ik_solver.chains)
												.Select(x=>x.GetComponent<BoneController>())
												.ToArray();
			}
		}
        additive_parent_transform = additive_parent.transform;
		UpdatePrevTransform();
	}

	/// <summary>
	/// ボーン変形
	/// </summary>
    public void Process()
    {
        if (null != additive_parent && CheckAdditiveParentMovedPreviewFrame())
        {
            ProcessAdditiveParent();
        }
    }

    bool CheckAdditiveParentMovedPreviewFrame()
    {
        // 付与親が動いたら更新する
        if (prev_parent_global_.position == additive_parent_transform.position)
            return true;
        if (prev_parent_global_.rotation == additive_parent_transform.rotation)
            return true;
        return false;
    }

    void ProcessAdditiveParent()
    {
        //付与親有りなら
        LiteTransform additive_parent_transform = additive_parent.GetDeltaTransform(add_local);
        AddMove(ref additive_parent_transform);
        AddRotate(ref additive_parent_transform);
    }

    void AddMove(ref LiteTransform additive_parent_transform)
    {
        if (add_move)
        {
            //付与移動有りなら
            transform.localPosition += additive_parent_transform.position * additive_rate;
        }
    }

    void AddRotate(ref LiteTransform additive_parent_transform)
    {
        if (add_rotate)
        {
            //付与回転有りなら
            Quaternion delta_rotate_rate;
            if (0.0f <= additive_rate)
            {
                //正回転
                delta_rotate_rate = Quaternion.Slerp(Quaternion.identity, additive_parent_transform.rotation, additive_rate);
            }
            else
            {
                //逆回転
                Quaternion additive_parent_delta_rotate_reverse = Quaternion.Inverse(additive_parent_transform.rotation);
                delta_rotate_rate = Quaternion.Slerp(Quaternion.identity, additive_parent_delta_rotate_reverse, -additive_rate);
            }
            transform.localRotation *= delta_rotate_rate;
        }
    }

    /// <summary>
    /// 差分トランスフォーム取得
    /// </summary>
    /// <returns>差分トランスフォーム</returns>
    /// <param name='is_add_local'>ローカル付与か(true:ローカル付与, false:通常付与)</param>
    LiteTransform GetDeltaTransform(bool is_add_local)
    {
        if (is_add_local)
        {
            //ローカル付与(親も含めた変形量算出)
            return new LiteTransform(transform.position - prev_global_.position
                                    , Quaternion.Inverse(prev_global_.rotation) * transform.rotation
                                    );
        }
        else
        {
            //通常付与(このボーン単体での変形量算出)
            return new LiteTransform(transform.localPosition - prev_global_.position
                                    , Quaternion.Inverse(prev_global_.rotation) * transform.localRotation
                                    );
        }
    }
	
	/// <summary>
	/// 差分基点トランスフォーム更新
	/// </summary>
	public void UpdatePrevTransform() {
        if (!add_local)
        {
            prev_global_ = new LiteTransform(transform.position, transform.rotation);
            prev_parent_global_ = new LiteTransform(additive_parent_transform.position, additive_parent_transform.rotation);
        }
        else
        {
            prev_global_ = new LiteTransform(transform.localPosition, transform.localRotation);
            prev_parent_global_ = new LiteTransform(additive_parent_transform.localPosition, additive_parent_transform.localRotation);
        }
	}
}
