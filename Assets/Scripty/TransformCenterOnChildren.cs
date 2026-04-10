using UnityEditor;
using UnityEngine;

public class TransformCenterOnChildren : Editor
{
    [MenuItem("Tools/坐标归到子物体中心点位置")]
    static void Start()
    {
        GameObject[] seGO = Selection.gameObjects;
        if (seGO.Length == 0)
        {
            return;
        }
        for (int i = 0; i < seGO.Length; i++)
        {
            Transform curTr = seGO[i].transform;
            Vector3 curPos = curTr.position;

            Vector3 cenPos = Center(curTr);

            int childCount = curTr.childCount;

            for (int k = 0; k < childCount; k++)
            {
                Vector3 offset = cenPos - curPos;
                curTr.GetChild(k).position -= offset;
            }
            Undo.RecordObject(curTr, "变换当前位置");
            curTr.position = cenPos;
        }
    }

    /// <summary>
    /// 计算子层级的物体中心点
    /// </summary>
    /// <param name="parentTransform">当前的 Transform</param>
    /// <returns>返回子物体的中心点</returns>
    static Vector3 Center(Transform parentTransform)
    {
        // 获取当前 Transform 的所有子物体
        Transform[] allTrans = parentTransform.GetComponentsInChildren<Transform>(true);
        if (allTrans.Length == 1)
        {
            return parentTransform.position;
        }

        //初始化初始值
        float xMax = allTrans[1].position.x,
            yMax = allTrans[1].position.y,
            zMax = allTrans[1].position.z,
            xMin = allTrans[1].position.x,
            yMin = allTrans[1].position.y,
            zMin = allTrans[1].position.z;



        foreach (var tran in allTrans)
        {
            if (tran.TryGetComponent(out Renderer renderer))
            {
                // 获取子物体包围盒中心点（在世界坐标系中）
                Vector3 childBoundsCenter = renderer.bounds.center;
                // 计算所有子物体中心点的最大最小值
                xMax = Mathf.Max(childBoundsCenter.x, xMax);
                yMax = Mathf.Max(childBoundsCenter.y, yMax);
                zMax = Mathf.Max(childBoundsCenter.z, zMax);

                xMin = Mathf.Min(childBoundsCenter.x, xMin);
                yMin = Mathf.Min(childBoundsCenter.y, yMin);
                zMin = Mathf.Min(childBoundsCenter.z, zMin);
            }
            else if (tran.GetComponents<Component>().Length > 1 || tran.childCount == 0)
            {
                // 如果当前物体的 Component 数量除了 Transform 以外还有其他 Component ，或者子物体是空的，也考虑在内
                xMax = Mathf.Max(tran.position.x, xMax);
                yMax = Mathf.Max(tran.position.y, yMax);
                zMax = Mathf.Max(tran.position.z, zMax);

                xMin = Mathf.Min(tran.position.x, xMin);
                yMin = Mathf.Min(tran.position.y, yMin);
                zMin = Mathf.Min(tran.position.z, zMin);
            }
        }

        // 组合大小值
        Vector3 min = new Vector3(xMin, yMin, zMin);
        Vector3 max = new Vector3(xMax, yMax, zMax);
        // 计算中心点
        Vector3 averageBoundsCenter = (min + max) / 2;

        return averageBoundsCenter;
    }
}