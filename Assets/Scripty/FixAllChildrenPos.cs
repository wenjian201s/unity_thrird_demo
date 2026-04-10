using UnityEngine;

public class FixAllChildrenPos : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 newParentPos = new Vector3(5, 5, 5);
            MoveParentKeepAllChildrenPos(newParentPos);
        }
    }

    void MoveParentKeepAllChildrenPos(Vector3 parentNewPos)
    {
        int childCount = transform.childCount;
        Vector3[] childrenPos = new Vector3[childCount];

        // 1. 缓存所有子对象的世界坐标
        for (int i = 0; i < childCount; i++)
        {
            childrenPos[i] = transform.GetChild(i).position;
        }

        // 2. 移动父对象
        transform.position = parentNewPos;

        // 3. 恢复所有子对象的世界坐标
        for (int i = 0; i < childCount; i++)
        {
            transform.GetChild(i).position = childrenPos[i];
        }
    }
}