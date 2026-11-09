using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Breach.UI
{
    // HTML B at its native 1280 x 720. The canvas scales the whole composition.
    public sealed class TitleBScreen : MonoBehaviour
    {
        BattleController c; TitleBBackdrop backdrop; RectTransform root, art;
        readonly List<Button> menu = new List<Button>();
        readonly List<MenuSignal> signals = new List<MenuSignal>();
        MenuSignalNoise menuNoise;
        CanvasGroup logo, menuGroup; GameObject modal; Coroutine intro;
        int focused; bool ready; float elapsed;
        public bool ModalOpen => modal && modal.activeSelf;
        public bool Ready => ready;
        public float IntroTime => elapsed;
        public float IntroPlaybackSpeed {get;set;}=1;
        public bool MenuSignalAtRest => signals.TrueForAll(s=>s.AtRest)&&menuNoise.TimeValue<0;
        public Button[] Menus => menu.ToArray();
        public void Initialize(BattleController controller)
        {
            c=controller;
            if(c.title) { c.title.SetActive(false); Destroy(c.title); }
            if(c.loading) { c.loading.SetActive(false); Destroy(c.loading); c.loading=null; }
            root=Ui.Rect("메인 - 타이틀 B",c.stage,0,0,1280,720);root.localScale=Vector3.one*1.5f;c.title=root.gameObject;
            art=Ui.Rect("B 배경 - 충돌과 회로",root,0,0,1280,720);
            backdrop=art.gameObject.AddComponent<TitleBBackdrop>();backdrop.raycastTarget=false;
            var text=Ui.Rect("B 중앙 로고",root,0,0,1280,720);logo=text.gameObject.AddComponent<CanvasGroup>();logo.blocksRaycasts=false;
            Label(text,"BREACH",198,145,135,8,new Color(.945f,.969f,1),true);
            Label(text,"PROTOCOL",328,80,59,20,new Color(.74f,.81f,.86f),true);
            Label(text,"TWO NETWORKS. ONE SURVIVOR.",85,24,14,5,new Color(.66f,.82f,.86f),true);
            Label(text,"침투 프로토콜",403,36,20,0,new Color(.76f,.85f,.89f));
            var menus=Ui.Rect("메뉴 - 세로 한 줄",root,0,0,1280,720);menuGroup=menus.gameObject.AddComponent<CanvasGroup>();
            string[] names={"침투 개시","플레이 방법","환경 설정","종료"};
            for(int i=0;i<4;i++)menu.Add(TextButton(menus,names[i],480+i*46,20));
            c.startButton=menu[0];var primary=menu[0].colors;primary.normalColor=primary.disabledColor=Ui.Cyan;menu[0].colors=primary;
            foreach(var button in menu){var signal=button.gameObject.AddComponent<MenuSignal>();signal.Initialize(button.GetComponent<TMP_Text>());signals.Add(signal);}
            menuNoise=Ui.Rect("메뉴 신호 잡음",menus,460,480,360,172).gameObject.AddComponent<MenuSignalNoise>();menuNoise.raycastTarget=false;
            menu[1].onClick.AddListener(ShowHelp);menu[2].onClick.AddListener(ShowSettings);menu[3].onClick.AddListener(Application.Quit);
            for(int i=0;i<4;i++){var nav=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=menu[(i+3)%4],selectOnDown=menu[(i+1)%4]};menu[i].navigation=nav;}
            root.SetSiblingIndex(c.result.transform.GetSiblingIndex());
            if(System.Array.FindIndex(System.Environment.GetCommandLineArgs(),a=>a.EndsWith("-check")||a=="--smoke-dir")<0 && PlayerPrefs.HasKey("WindowWidth")) {
                int width=PlayerPrefs.GetInt("WindowWidth",1600);if(width!=1280&&width!=1600&&width!=1920)width=1600;
                Screen.SetResolution(width,width*9/16,PlayerPrefs.GetInt("Fullscreen",Screen.fullScreen?1:0)==1);
            }
        }
        TMP_Text Label(Transform parent,string value,float y,float h,float size,float spacing,Color color,bool latin=false)
        {
            var t=Ui.Text(value,parent,100,y,1080,h,value,latin&&c.titleFont?c.titleFont:c.font,size,color);
            t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;
            // TMP spacing is in hundredths of the current em, whereas SVG uses px.
            t.characterSpacing=spacing*100/size;return t;
        }
        Button TextButton(Transform parent,string value,float y,float size)
        {
            var t=Ui.Text(value,parent,460,y,360,34,value,c.font,size,Color.white);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=true;
            var button=t.gameObject.AddComponent<Button>();button.targetGraphic=t;
            var colors=button.colors;colors.normalColor=new Color(.68f,.81f,.85f);colors.highlightedColor=Color.white;colors.selectedColor=Color.white;colors.pressedColor=Ui.Gold;colors.disabledColor=colors.normalColor;colors.fadeDuration=.15f;button.colors=colors;
            // No ButtonFeedback: that component deliberately creates a border for HUD buttons.
            return button;
        }
        public void Show()
        {
            FinishIntro();if(modal)modal.SetActive(false);root.gameObject.SetActive(true);ready=false;focused=0;
            intro=StartCoroutine(Animate());
        }
        IEnumerator Animate()
        {
            menuGroup.interactable=false;menuGroup.blocksRaycasts=false;
            menuGroup.alpha=1;
            for(elapsed=0;elapsed<4.5f;elapsed+=Time.unscaledDeltaTime*IntroPlaybackSpeed)
            {
                backdrop.TimeValue=elapsed;backdrop.SetVerticesDirty();
                logo.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.1f,2.85f,elapsed));
                for(int i=0;i<signals.Count;i++)signals[i].SetTime(elapsed-3-i*.07f,i);
                menuNoise.TimeValue=elapsed-3;menuNoise.SetVerticesDirty();
                float hit=Mathf.Clamp01((elapsed-TitleBBackdrop.ContactTime)/.38f);
                art.anchoredPosition=elapsed>=TitleBBackdrop.ContactTime&&hit<1?new Vector2(Mathf.Sin(hit*62)*14,Mathf.Sin(hit*43)*8)*Mathf.Pow(1-hit,2):Vector2.zero;
                yield return null;
            }
            intro=null;FinishIntro();
        }
        public void FinishIntro()
        {
            if(intro!=null){StopCoroutine(intro);intro=null;}if(!root)return;
            elapsed=4.5f;backdrop.TimeValue=4.5f;backdrop.SetVerticesDirty();art.anchoredPosition=Vector2.zero;
            logo.alpha=menuGroup.alpha=1;menuGroup.interactable=menuGroup.blocksRaycasts=true;ready=true;
            for(int i=0;i<signals.Count;i++)signals[i].SetTime(1,i);
            menuNoise.TimeValue=-1;menuNoise.SetVerticesDirty();
        }
        public void HandleKeys()
        {
            if(Input.GetKeyDown(KeyCode.Escape)){if(ModalOpen)CloseModal();else if(!ready)FinishIntro();return;}
            if(!ready){if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))FinishIntro();return;}
            // EventSystem handles submit/navigation after focus is set. Only bootstrap focus here.
            if(!ModalOpen && !EventSystem.current.currentSelectedGameObject && (Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.UpArrow)))
                EventSystem.current.SetSelectedGameObject(menu[focused].gameObject);
        }
        RectTransform OpenModal(string title)
        {
            if(modal)Destroy(modal);var panel=Ui.Box("메인 - "+title,root,250,130,780,475,new Color(.025f,.045f,.065f,.99f));panel.raycastTarget=true;modal=panel.gameObject;
            Ui.Box("모달 상단 구분",panel.transform,32,30,716,2,Ui.Cyan);
            var head=Ui.Text("제목",panel.transform,32,48,716,45,title,c.font,28,Color.white);head.alignment=TextAlignmentOptions.Center;
            menuGroup.interactable=false;menuGroup.blocksRaycasts=false;
            return panel.rectTransform;
        }
        Button ModalButton(Transform parent,string text,float y,UnityEngine.Events.UnityAction action)
        {
            var b=TextButton(parent,text,y,20);((RectTransform)b.transform).anchoredPosition=new Vector2(210,-y);b.onClick.AddListener(action);return b;
        }
        public void CloseModal(){if(modal)modal.SetActive(false);menuGroup.interactable=menuGroup.blocksRaycasts=true;EventSystem.current.SetSelectedGameObject(menu[focused].gameObject);}
        public void ShowHelp()
        {
            focused=1;var panel=OpenModal("플레이 방법");
            var body=Ui.Text("플레이 설명",panel,45,118,690,255,"1. 카드를 선택한 뒤 서버·가로 연결 칸을 누르거나 끌어 놓습니다.\n2. 코드 주입은 장전, 라우팅 변경은 가로줄 설치·제거입니다.\n3. 버퍼는 카드를 뽑습니다. 강화 카드는 전투 때 공개됩니다.\n4. 행동 종료 후 양쪽 패킷이 사다리를 따라 이동합니다.\n5. 상대 서버 3개를 모두 파괴하면 승리합니다.\n\n같은 턴의 행동 취소가 가능합니다. 카드 효과는 카드 설명을 확인하세요.",c.font,18,new Color(.78f,.85f,.9f));body.lineSpacing=12;
            var close=ModalButton(panel,"돌아가기",410,CloseModal);EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
        public void ShowSettings()
        {
            focused=2;var panel=OpenModal("환경 설정");
            Button display=null;display=ModalButton(panel,Screen.fullScreen?"전체 화면 → 창 모드":"창 모드 → 전체 화면",140,()=>{bool full=!Screen.fullScreen;Screen.fullScreen=full;PlayerPrefs.SetInt("Fullscreen",full?1:0);if(!PlayerPrefs.HasKey("WindowWidth"))PlayerPrefs.SetInt("WindowWidth",1600);PlayerPrefs.Save();display.GetComponent<TMP_Text>().text=full?"전체 화면 → 창 모드":"창 모드 → 전체 화면";});
            int[] widths={1280,1600,1920};for(int i=0;i<3;i++){int width=widths[i];ModalButton(panel,width+" × "+(width*9/16),205+i*48,()=>{Screen.SetResolution(width,width*9/16,Screen.fullScreen);PlayerPrefs.SetInt("WindowWidth",width);PlayerPrefs.SetInt("Fullscreen",Screen.fullScreen?1:0);PlayerPrefs.Save();});}
            ModalButton(panel,"돌아가기",410,CloseModal);EventSystem.current.SetSelectedGameObject(display.gameObject);
        }
        void OnDestroy(){if(intro!=null)StopCoroutine(intro);}
    }
}
