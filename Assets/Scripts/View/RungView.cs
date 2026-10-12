using LadderBattle.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 가로줄(연결 회선) 한 줄. `Assets/Prefabs/가로줄.prefab`.
    /// 놓고 부수고 하며 개수가 바뀌므로 프리팹으로 두고 실행 중에 만든다.
    /// </summary>
    public class RungView : MonoBehaviour
    {
        [Tooltip("줄 자체")] public Image 막대;
        [Tooltip("네온 번짐")] public Image 번짐;

        public void 보이기(Side 주인)
        {
            Color c = Palette.편(주인);
            if (막대 != null) 막대.color = c;
            if (번짐 != null) 번짐.color = new Color(c.r, c.g, c.b, 0.6f);
        }
    }
}
