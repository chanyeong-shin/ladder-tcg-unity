using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Breach.UI;
namespace Breach.EditorTools {
 public static class SceneBuilder {
  [Serializable]class Layout {public float width,height;public Item[] items;}
  [Serializable]class Item {public string name,id,type,text,align,vertical,sprite;public int parent;public float x,y,w,h,weight,font;public float[] fill,stroke;}
  static TMP_FontAsset font;
  static Color C(float[] a){return a==null?Color.clear:new Color(a[0],a[1],a[2],a[3]);}
  static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
  static RectTransform Import(string name,Transform parent){var data=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Figma/"+name+".json"));var nodes=new RectTransform[data.items.Length];
   for(int i=0;i<data.items.Length;i++){var item=data.items[i];if(item.name=="Source_64_282"||item.name=="Source_64_283")continue;Transform p=item.parent<0?parent:nodes[item.parent];if(item.parent>=0&&!p)continue;float x=item.x,y=item.y;if(item.parent>=0){x-=data.items[item.parent].x;y-=data.items[item.parent].y;}
    var node=Ui.Rect(item.name,p,x,y,Mathf.Max(.1f,item.w),Mathf.Max(.1f,item.h));nodes[i]=node;
    if(item.type=="TEXT"){var t=node.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=item.text;t.fontSize=item.font;t.color=C(item.fill);t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;t.alignment=item.align=="CENTER"?TextAlignmentOptions.Top: item.align=="RIGHT"?TextAlignmentOptions.TopRight:TextAlignmentOptions.TopLeft;t.margin=Vector4.zero;continue;}
    if(!string.IsNullOrEmpty(item.sprite)){var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Figma/"+item.sprite+".png");if(!sprite)throw new Exception("Missing exported vector "+item.sprite);var image=node.gameObject.AddComponent<Image>();image.sprite=sprite;image.raycastTarget=false;continue;}
    int sides=item.type=="ELLIPSE"?32:item.type=="REGULAR_POLYGON"?6:4;
    if(C(item.fill).a>0){var shape=node.gameObject.AddComponent<Shape>();shape.color=C(item.fill);shape.sides=sides;shape.raycastTarget=false;}
    if(C(item.stroke).a>0)Ui.Box("외곽선",node,0,0,item.w,item.h,C(item.stroke),false,item.weight,sides);
   }
   return nodes[0];
  }
  static Button Button(string name,Transform parent,float x,float y,float w,float h,string caption,bool visible=true){var rect=Ui.Rect(name,parent,x,y,w,h);var img=rect.gameObject.AddComponent<Image>();img.color=visible?new Color(.08f,.18f,.22f,.95f):Color.clear;var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=img;var colors=b.colors;colors.highlightedColor=new Color(.4f,.9f,1);colors.pressedColor=Ui.Gold;colors.disabledColor=new Color(.22f,.25f,.28f,.45f);b.colors=colors;
   if(!string.IsNullOrEmpty(caption)){var text=Ui.Text("버튼 글자",rect,0,0,w,h,caption,font,20,Color.white);text.alignment=TextAlignmentOptions.Center;}
   return b;
  }
  static void Tint(Transform root){foreach(var g in root.GetComponentsInChildren<Graphic>(true)){var c=g.color;if(c.b>c.r*1.2f&&c.g>c.r*1.2f)g.color=new Color(c.b,c.r*.6f,c.g*.55f,c.a);}}
  static CardView CardPrefab(Card type){string template=type==Card.Reroute?"reroute":type==Card.Firewall?"firewall":type==Card.ZeroDay?"zero":"inject";var root=Import(template,null);root.name="카드 - "+Cards.Names[(int)type];Place(root,0,0,126,201.6f);
   var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;var view=root.gameObject.AddComponent<CardView>();view.card=type;
   // Replace fragmented captions with one editable body, preserving the approved chip artwork.
   foreach(var t in root.GetComponentsInChildren<TMP_Text>(true)){if(t.name=="Card_Name"){t.text=Cards.Names[(int)type];Place(t.rectTransform,11.76f,110.88f,103,13);}if(t.name=="Card_English"){t.text=Cards.English[(int)type];Place(t.rectTransform,11.76f,128.52f,103,10);}if(t.name.StartsWith("Effect_")||t.name=="Upgrade_From")UnityEngine.Object.DestroyImmediate(t.gameObject);}
   var body=Ui.Text("능력 설명",root,11.76f,146,103,50,Cards.Description[(int)type],font,6.3f,new Color(.75f,.85f,.9f));body.overflowMode=TextOverflowModes.Truncate;body.textWrappingMode=TextWrappingModes.Normal;
   if(type==Card.Buffer||type==Card.Overflow){foreach(Transform ch in root.Cast<Transform>().ToArray()){var r=ch as RectTransform;float y=-r.anchoredPosition.y;if(y>28&&y<105&&r.sizeDelta.y<90)UnityEngine.Object.DestroyImmediate(ch.gameObject);}Color ink=type==Card.Overflow?Ui.Gold:Ui.Cyan;for(int i=0;i<(type==Card.Buffer?2:4);i++){Ui.Box("메모리 뱅크 "+i,root,30+i*14,44,11,43,ink,false,1);for(int pin=0;pin<5;pin++)Ui.Box("메모리 접점",root,27+i*14,49+pin*7,3,2,ink);}if(type==Card.Overflow)Ui.Box("권한 상승 레일",root,3,3,120,195,Ui.Gold,false,1);}
   string path="Assets/Prefabs/Card_"+type+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,path);UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab.GetComponent<CardView>();
  }
  static ServerView ServerPrefab(int owner){var root=Ui.Rect(owner==0?"내 서버":"상대 서버",null,0,0,100,110);var s=root.gameObject.AddComponent<ServerView>();s.owner=owner;
   var normal=Import("server",root);var empty=Import("serverNull",root);var offline=Import("offline",root);s.normal=normal.gameObject;s.empty=empty.gameObject;s.offline=offline.gameObject;
   Ui.Box("장전 발광 - 바깥",normal,13,18,74,74,new Color(Ui.Cyan.r,Ui.Cyan.g,Ui.Cyan.b,.06f),false,8,6).transform.SetAsFirstSibling();
   Ui.Box("장전 발광 - 안쪽",normal,19,24,62,62,new Color(Ui.Cyan.r,Ui.Cyan.g,Ui.Cyan.b,.12f),false,4,6).transform.SetAsFirstSibling();
   s.number=normal.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="장전량");Place(s.number.rectTransform,0,0,100,110);s.number.alignment=TextAlignmentOptions.Center;
   if(owner==1){Tint(normal);Tint(empty);}normal.gameObject.SetActive(false);empty.gameObject.SetActive(true);offline.gameObject.SetActive(false);
   var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Prefabs/Server_"+owner+".prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab.GetComponent<ServerView>();
  }
  [MenuItem("Breach/승인 화면으로 새 씬 생성")]
  public static void Create(){
   AssetDatabase.Refresh();Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Prefabs");
   foreach(string path in Directory.GetFiles("Assets/Art/Figma","*.png")){var t=(TextureImporter)AssetImporter.GetAtPath(path);if(t.textureType!=TextureImporterType.Sprite){t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;t.SaveAndReimport();}}
   font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Regular SDF.asset");if(!font)throw new Exception("Korean font missing");
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var camera=new GameObject("화면 카메라",typeof(Camera));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=Color.black;
   var canvas=new GameObject("화면 Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scale=canvas.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
   var stage=Ui.Rect("16대9 게임 영역",canvas.transform,0,0,1920,1080);stage.anchorMin=stage.anchorMax=new Vector2(.5f,.5f);stage.pivot=new Vector2(0,1);stage.anchoredPosition=new Vector2(-960,540);
   var eventSystem=new GameObject("입력 EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
   var manager=new GameObject("게임 진행",typeof(BattleController));var c=manager.GetComponent<BattleController>();c.stage=stage;c.font=font;
   var game=Ui.Rect("대전 화면",stage,0,0,1920,1080);c.game=game.gameObject;
   c.backgrounds=new[]{Import("screen",game).gameObject,Import("enemy",game).gameObject,Import("resolve",game).gameObject};foreach(var bg in c.backgrounds)Place((RectTransform)bg.transform,0,0,1920,1080);
   // The sample's disabled server meter is replaced by live status strips.
   foreach(var bg in c.backgrounds){foreach(string name in new[]{"Source_64_364","Source_64_367","Source_64_369","Source_64_479","Source_64_482","Source_64_485","Source_64_490","Source_64_493","Source_64_496"}){var t=BattleController.Find(bg.transform,name);if(t)t.gameObject.SetActive(false);}}
   c.rungs=Ui.Rect("사다리 - 실제 연결 회선",game,0,0,1920,1080);c.paths=Ui.Rect("사다리 - 경로 미리보기",game,0,0,1920,1080);
   c.servers=new ServerView[6];for(int owner=0;owner<2;owner++){var prefab=ServerPrefab(owner);for(int channel=0;channel<3;channel++){var server=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,game);var view=server.GetComponent<ServerView>();view.channel=channel;view.controller=c;var serverHit=server.AddComponent<Image>();serverHit.color=Color.clear;serverHit.raycastTarget=true;server.name=(owner==0?"아래":"위")+" CH "+(channel+1)+" 서버";Place((RectTransform)server.transform,652+280*channel,owner==0?722:146,100,110);c.servers[owner*3+channel]=view;}}
   c.laneTargets=new Button[3];for(int i=0;i<3;i++){var b=Button("CH "+(i+1)+" 대상",game,582+i*280,250,i==2?240:280,470,"",false);c.laneTargets[i]=b;var target=b.gameObject.AddComponent<TargetView>();target.controller=c;target.target=i;}
   c.rungTargets=new Button[6];for(int i=0;i<6;i++){var b=Button("연결 회선 자리 "+i,game,712+280*(i%2),BattleController.RowY[i/2]-32,260,64,"",false);c.rungTargets[i]=b;var target=b.gameObject.AddComponent<TargetView>();target.controller=c;target.target=i;target.rung=true;}
   c.hand=Ui.Rect("하단 - 손패",game,0,0,1920,1080);c.cardPrefabs=Enum.GetValues(typeof(Card)).Cast<Card>().Select(CardPrefab).ToArray();
   var back=Import("back",null);back.name="카드 공통 뒷면";Place(back,0,0,100,160);c.backPrefab=PrefabUtility.SaveAsPrefabAsset(back.gameObject,"Assets/Prefabs/Card_Back.prefab");UnityEngine.Object.DestroyImmediate(back.gameObject);
   c.endButton=Button("오른쪽 - 행동 종료",game,1459,661,287,60,"행동 종료");c.undoButton=Button("오른쪽 - 행동 취소",game,1459,733,137,40,"행동 취소");c.logButton=Button("오른쪽 - 기록",game,1608,733,138,40,"기록 보기 +");
   c.roundLabel=Ui.Text("라운드 번호",game,173,83,230,25,"ROUND 01",font,18,new Color(.55f,.7f,.76f));
   c.statusLabel=Ui.Text("조작 안내",game,1459,795,287,100,"카드를 선택하세요",font,16,new Color(.65f,.8f,.85f));c.statusLabel.alignment=TextAlignmentOptions.TopLeft;
   c.secretLabel=Ui.Text("비공개 카드 수",game,582,106,800,20,"",font,13,Ui.Gold);c.secretLabel.alignment=TextAlignmentOptions.Right;
   var panel=Ui.Box("기록 패널",game,1400,380,420,265,new Color(.025f,.04f,.055f,.98f));c.logPanel=panel.gameObject;Ui.Box("기록 테두리",panel.transform,0,0,420,265,new Color(.2f,.4f,.45f),false);c.logText=Ui.Text("기록 본문",panel.transform,16,12,388,244,"",font,12,new Color(.7f,.82f,.86f));
   c.fx=Ui.Rect("연출 오버레이",game,0,0,1920,1080);
   var cover=Import("cover",stage);cover.name="시작 표지";cover.localScale=Vector3.one*1.5f;Place(cover,0,0,1280,720);c.title=cover.gameObject;var start=BattleController.Find(cover,"Start_Button");c.startButton=start.gameObject.AddComponent<Button>();c.startButton.targetGraphic=start.GetComponent<Graphic>();c.startButton.targetGraphic.raycastTarget=true;
   var load=Import("loading",stage);load.name="접속 로딩";load.localScale=Vector3.one*1.5f;Place(load,0,0,1280,720);c.loading=load.gameObject;Ui.Text("접속 안내",load,260,590,760,45,"서버 연결 중 / ESTABLISHING CONNECTION",font,18,Ui.Cyan);
   var result=Ui.Box("대전 결과",stage,0,0,1920,1080,new Color(.02f,.03f,.045f,.96f));result.raycastTarget=true;c.result=result.gameObject;c.resultLabel=Ui.Text("결과",result.transform,300,410,1320,90,"ACCESS GRANTED",font,42,Ui.Cyan);c.resultLabel.alignment=TextAlignmentOptions.Center;c.retryButton=Button("다시 침투",result.transform,770,550,380,65,"다시 시작 / RECONNECT");
   manager.AddComponent<SmokeCheck>();c.backgrounds[1].SetActive(false);c.backgrounds[2].SetActive(false);c.title.SetActive(true);c.loading.SetActive(false);c.result.SetActive(false);c.logPanel.SetActive(false);c.game.SetActive(false);
   EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/Game.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Game.unity",true)};PlayerSettings.productName="Breach Protocol";PlayerSettings.companyName="Breach Team";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;AssetDatabase.SaveAssets();Debug.Log("BREACH_SCENE_CREATED: editable UI and prefabs");
  }
  public static void Rebuild(){try{Create();Build();}catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}}
  [MenuItem("Breach/저장된 씬으로 Windows 빌드")]
  public static void Build(){try{var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Game.unity"},locationPathName="Build/BreachProtocol.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});File.WriteAllText("Build/build-result.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors);if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded?0:1);}catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}}
 }
}
