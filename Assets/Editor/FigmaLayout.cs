using System;
using UnityEngine;

namespace LadderBattle.EditorTools
{
    /// <summary>
    /// 피그마 배치표 한 줄. `_검증/figma_to_unity.py` 가 만든 JSON 과 이름이 1:1로 맞는다.
    ///
    /// 나무 구조가 아니라 납작한 목록인 이유 — 유니티의 JsonUtility 는 자기 자신을
    /// 품은 형을 제대로 못 읽는다. 그래서 부모를 <see cref="p"/> 번호로 가리킨다.
    /// 이름이 짧은 것은 파일 크기 때문이다. 뜻은 아래 주석에 적는다.
    /// </summary>
    [Serializable]
    public class FigmaNode
    {
        public int i;            // 내 번호 (목록에서의 자리)
        public int p;            // 부모 번호. 뿌리는 -1
        public string id;        // 피그마 노드 ID. 시안과 대조할 때 쓴다
        public string name;
        public string type;      // FRAME RECTANGLE TEXT ELLIPSE REGULAR_POLYGON GROUP

        public float x, y, w, h; // 프레임 왼쪽 위가 0,0 인 좌표

        public string fill;      // "#rrggbb" 또는 빈 문자열
        public float fillA;
        public string stroke;
        public float strokeA;
        public float strokeW;
        public float radius;     // 이 시안에는 전부 0이다
        public float opacity;
        public bool clip;

        public string glow;      // 네온 번짐 (피그마에서는 자리를 안 옮긴 그림자)
        public float glowA;
        public float glowR;

        public int sides;        // 정다각형 꼭짓점 수. 0이면 다각형이 아니다

        public string text;
        public string font;      // Inter / Rajdhani
        public float fontSize;
        public int fontWeight;
        public float lineH;      // 줄 간격 (px)
        public float letter;     // 자간 (px)
        public string align;     // LEFT / CENTER / RIGHT
        public string valign;    // TOP / CENTER / BOTTOM

        public bool HasFill => !string.IsNullOrEmpty(fill) && fillA > 0.004f;
        public bool HasStroke => !string.IsNullOrEmpty(stroke) && strokeA > 0.004f && strokeW > 0f;
        public bool HasGlow => !string.IsNullOrEmpty(glow) && glowA > 0.004f && glowR > 0f;
    }

    /// <summary>프레임 하나치 배치표.</summary>
    [Serializable]
    public class FigmaFrame
    {
        public string name;
        public float width, height;
        public FigmaNode[] nodes;
    }
}
