using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    // Full-opacity, discrete signal interruptions, not a fade-in. The resting
    // label and its button retain exactly the original position and hit box.
    public sealed class MenuSignal : MonoBehaviour
    {
        CanvasGroup group;
        RectTransform rect;
        Vector2 home;
        TMP_Text[] ghosts=new TMP_Text[2];
        RectTransform[] clips=new RectTransform[2];
        public bool Distorting {get;private set;}
        public float VisibleAlpha => group.alpha;
        public void Initialize(TMP_Text source)
        {
            rect=(RectTransform)transform;home=rect.anchoredPosition;
            group=gameObject.AddComponent<CanvasGroup>();
            for(int i=0;i<2;i++){
                clips[i]=Ui.Rect("메뉴 색 분리 "+i,rect,0,7+i*13,360,8);
                clips[i].gameObject.AddComponent<RectMask2D>();
                ghosts[i]=Ui.Text("분리된 글자",clips[i],0,-7-i*13,360,34,source.text,source.font,source.fontSize,i==0?Ui.Cyan:Ui.Red);
                ghosts[i].alignment=TextAlignmentOptions.Center;ghosts[i].textWrappingMode=TextWrappingModes.NoWrap;
            }
            SetTime(-1,0);
        }
        public void SetTime(float local,int index)
        {
            // 0.44 seconds per row: short on/off bursts, a torn signal, then lock.
            Distorting=local>=0&&local<.44f;
            if(!Distorting){group.alpha=local<0?0:1;rect.anchoredPosition=home;foreach(var t in ghosts)t.gameObject.SetActive(false);return;}
            int tick=Mathf.FloorToInt(local*46);
            group.alpha=tick==1||tick==4||tick==5||tick==10?0:1;
            float amount=local<.29f?1:.35f;
            float dx=((tick*17+index*11)%9-4)*2.2f*amount;
            rect.anchoredPosition=home+new Vector2(dx,tick%5==0?1:0);
            for(int i=0;i<2;i++){
                ghosts[i].gameObject.SetActive(true);
                float cy=5+(tick*7+i*13)%23;
                clips[i].anchoredPosition=new Vector2(0,-cy);
                ghosts[i].rectTransform.anchoredPosition=new Vector2((i==0?-1:1)*(5+tick%4*3)*amount,cy);
                var ink=i==0?Ui.Cyan:Ui.Red;ghosts[i].color=new Color(ink.r,ink.g,ink.b,.8f);
            }
        }
        public bool AtRest => !Distorting&&group.alpha==1&&rect.anchoredPosition==home&&System.Array.TrueForAll(ghosts,g=>!g.gameObject.activeSelf);
    }
    public sealed class MenuSignalNoise : MaskableGraphic
    {
        public float TimeValue=-1;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for(int row=0;row<4;row++){
                float local=TimeValue-row*.07f;if(local<0||local>=.44f)continue;
                int tick=Mathf.FloorToInt(local*46);
                for(int n=0;n<9;n++){
                    int seed=(tick*31+n*53+row*7)%137;
                    float x=90+seed*1.2f,y=row*46+5+(seed*13)%25,w=3+seed%23,h=n%3==0?2:1;
                    Color ink=n%2==0?Ui.Cyan:Ui.Red;ink.a=n%3==0?.65f:.3f;
                    int start=vh.currentVertCount;
                    vh.AddVert(new Vector3(x,-y),ink,Vector2.zero);vh.AddVert(new Vector3(x+w,-y),ink,Vector2.zero);
                    vh.AddVert(new Vector3(x+w,-y-h),ink,Vector2.zero);vh.AddVert(new Vector3(x,-y-h),ink,Vector2.zero);
                    vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
                }
            }
        }
    }
}
