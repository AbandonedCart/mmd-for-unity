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

    public bool enable_local_axis = false;   // ローカル軸
    public Vector3 local_x;
    public Vector3 local_y;
    public Vector3 local_z;
    public Vector3 LocalRotationValues { get; private set; }
    Vector3 initial_local_x;
    Vector3 initial_local_y;
    Vector3 initial_local_z;

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
    LiteTransform prev_local_;

    LiteTransform prev_parent_transform_;
    Transform additive_parent_transform;
    Transform my_transform;     // 自分自身のTransform

	/// <summary>
	/// 初回更新前処理
	/// </summary>
	void Start()
	{
        my_transform = transform;
		if (null != ik_solver) {
			ik_solver = transform.GetComponent<CCDIKSolver>();
			if (0 == ik_solver_targets.Length) {
				ik_solver_targets = Enumerable.Repeat(ik_solver.target, 1)
												.Concat(ik_solver.chains)
												.Select(x=>x.GetComponent<BoneController>())
												.ToArray();
			}
		}

        if (additive_parent != null)
            additive_parent_transform = additive_parent.transform;
        initial_local_x = local_x;
        initial_local_y = local_y;
        initial_local_z = local_z;
        LocalRotationValues = Vector3.zero;
		UpdatePrevTransform();
	}

	/// <summary>
	/// ボーン変形
	/// </summary>
    public void Process()
    {
        if (null != additive_parent)
        {
            //付与親有りなら
            LiteTransform additive_parent_transform = additive_parent.GetDeltaTransform(add_local);
            if (add_move && CheckAdditiveParentMovedPreviewFrame())
            {
                //付与移動有りなら
                my_transform.localPosition += additive_parent_transform.position * additive_rate;
            }
            if (add_rotate && CheckAdditiveParentRotatedPreviewFrame())
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
                my_transform.localRotation *= delta_rotate_rate;
            }
        }
    }

    bool CheckAdditiveParentMovedPreviewFrame()
    {
        // 付与親が動いたら更新する
        if (prev_parent_transform_.position != additive_parent_transform.position)
            return true;
        return false;
    }

    bool CheckAdditiveParentRotatedPreviewFrame()
    {
        if (prev_parent_transform_.rotation != additive_parent_transform.rotation)
            return true;
        return false;
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
            return new LiteTransform(my_transform.position - prev_global_.position
                                    , Quaternion.Inverse(prev_global_.rotation) * my_transform.rotation
                                    );
        }
        else
        {
            //通常付与(このボーン単体での変形量算出)
            return new LiteTransform(my_transform.localPosition - prev_local_.position
                                    , Quaternion.Inverse(prev_local_.rotation) * my_transform.localRotation
                                    );
        }
    }

    /// <summary>
    /// 差分基点トランスフォーム更新
    /// </summary>
    public void UpdatePrevTransform()
    {
        prev_global_ = new LiteTransform(my_transform.position, my_transform.rotation);
        prev_local_ = new LiteTransform(my_transform.localPosition, my_transform.localRotation);
        if (additive_parent != null)
            prev_parent_transform_ = new LiteTransform(additive_parent_transform.position, additive_parent_transform.rotation);
    }

    /// <summary>
    /// 変更したローカル軸を初期位置に戻し，ローカル回転も元に戻す
    /// </summary>
    public void ResetPose()
    {
        local_x = initial_local_x;
        local_y = initial_local_y;
        local_z = initial_local_z;
        my_transform.localRotation = Quaternion.identity;
        LocalRotationValues = Vector3.zero;
    }

    Quaternion RotatePoseVector(float angle, ref Vector3 a, ref Vector3 v1, ref Vector3 v2)
    {
        var q = Quaternion.identity;
        if (angle != 0f)
        {
            q = Quaternion.AngleAxis(angle, a);
            v1 = q * v1;
            v2 = q * v2;
        }
        return q;
    }

    /// <summary>
    /// ローカル軸から見てx, y, z軸回転を行い，乗算したものを返す
    /// </summary>
    /// <param name="x">X軸の回転量</param>
    /// <param name="y">Y軸の回転量</param>
    /// <param name="z">Z軸の回転量</param>
    /// <returns>各軸を回転させて乗算したもの</returns>
    public Quaternion RotatePose(float x, float y, float z)
    {
        var vz = local_z;
        var vy = local_y;
        var vx = local_x;

        // 姿勢の回転
        var qx = RotatePoseVector(x, ref vx, ref vy, ref vz);
        var qy = RotatePoseVector(y, ref vy, ref vz, ref vx);
        var qz = RotatePoseVector(z, ref vz, ref vx, ref vy);

        LocalRotationValues += new Vector3(x, y, z);

        // 新しい姿勢に変える
        local_x = vx;
        local_y = vy;
        local_z = vz;

        return qx * qy * qz;
    }
}
