using System.Collections;
using System.IO;
using System.Text;
using LadderBattle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LadderBattle.View
{
    /// <summary>
    /// 사람 없이 한 판을 끝까지 돌려 보는 검사 운전수.
    ///
    /// 왜 필요한가 — 컴파일이 통과하고 화면이 시안과 같아도 **끌어서 한 판이 끝나는지**는
    /// 다른 문제다. 그걸 손으로만 확인하면 고칠 때마다 사람이 붙어 있어야 한다.
    ///
    /// **카드는 사람과 똑같이 끌어서 낸다** — `OnBeginDrag` 부터 `OnEndDrag` 까지
    /// 실제 이벤트를 흘려보낸다. 엔진을 직접 부르면 훨씬 간단하지만 그러면 끌기 · 놓인
    /// 자리 찾기 · 칩 소켓 · 기판 회로 같은 길이 한 번도 지나가지 않는다.
    ///
    /// 평소에는 씬에 없다. `PlayTest` 가 검사할 때만 붙인다.
    /// </summary>
    public class AutoPlay : MonoBehaviour
    {
        public string 사진폴더 = "Logs/자동플레이";
        [Tooltip("낼지 넘길지 결정하는 횟수의 상한. 프레임 수가 아니다")]
        public int 최대결정 = 400;
        [Tooltip("이 시간을 넘기면 멈춘 것으로 본다(초)")] public float 최대시간 = 300f;
        [Tooltip("한 턴에 낼 카드 수")] public int 한턴최대 = 2;
        [Tooltip("끌기 대신 누르기로 낸다. 옛 길을 같이 확인할 때 켠다")]
        public bool 눌러서낸다;
        [Tooltip("연출을 끄고 숫자만 본다. 켜면 훨씬 빠르다")] public bool 연출생략;
        public bool 끝났다 { get; private set; }
        public string 보고 { get; private set; } = "";

        GameDirector _dir;
        ScreenFlow _flow;
        Camera _cam;
        readonly StringBuilder _log = new StringBuilder();

        // 연출이 실제로 돌았다는 증거. 한 판에 한 번씩만 찍는다
        bool _찍은명판, _찍은전체명판, _찍은예고, _찍은칩, _찍은회로, _찍은발동, _찍은끌기;
        int _본명판, _본칩, _본회로, _본예고;

        IEnumerator Start()
        {
            Application.targetFrameRate = -1;
            yield return null;

            _dir = FindFirstObjectByType<GameDirector>();
            _flow = FindFirstObjectByType<ScreenFlow>();
            if (_dir == null || _flow == null)
            {
                마침("GameDirector 나 ScreenFlow 를 못 찾았다");
                yield break;
            }
            _dir.연출생략 = 연출생략;
            if (연출생략) { _dir.봇지연 = 0f; _dir.발동지연 = 0f; }

            var canvas = FindFirstObjectByType<Canvas>();
            _cam = canvas != null ? canvas.worldCamera : Camera.main;

            찍기("01_시작화면");
            if (_flow.시작버튼 == null) { 마침("시작 버튼이 없다"); yield break; }
            _flow.시작버튼.onClick.Invoke();
            yield return null;

            var e = _dir.Engine;
            if (e == null) { 마침("새 판이 시작되지 않았다"); yield break; }
            줄($"새 판 · 1턴 공격 {(e.S.First == Side.P ? "나" : "봇")}");
            StartCoroutine(연출지켜보기());

            // 걸음을 프레임으로 세면 안 된다. 배치모드는 초당 수백~수천 프레임이 돌아서
            // 연출이 한 번 돌기도 전에 상한에 걸린다. **결정 횟수와 실제 시간**으로 센다
            int 결정 = 0, 찍은턴 = -1, 이번턴낸장수 = 0;
            float 시작 = Time.realtimeSinceStartup;
            while (!e.S.Ended && 결정 < 최대결정 && Time.realtimeSinceStartup - 시작 < 최대시간)
            {
                if (e.S.Turn != 찍은턴)
                {
                    찍은턴 = e.S.Turn;
                    이번턴낸장수 = 0;
                    if (e.S.Turn <= 3) 찍기($"02_{e.S.Turn}턴");
                }

                // 연출이 도는 동안은 아무것도 누르지 않는다. 사람도 그래야 한다
                if (_dir.연출중 || e.S.Acting != Side.P) { yield return null; continue; }

                // 손패를 한 턴에 다 쏟으면 한두 턴 만에 끝나서 검사가 볼 게 없다.
                // 사람이 두는 것처럼 몇 장만 내고 넘긴다
                bool 냈다 = false;
                결정++;
                if (이번턴낸장수 < 한턴최대) yield return 한장내기(ok => 냈다 = ok);
                if (냈다) 이번턴낸장수++;
                else { 턴넘기기(); 이번턴낸장수 = 0; }
                yield return null;
            }

            if (!e.S.Ended)
            {
                var hud = FindFirstObjectByType<HudView>();
                줄($"멈춘 자리 · 턴 {e.S.Turn} {e.S.Step} 행동 {e.S.Acting} · 연출중={_dir.연출중}"
                   + $" · 행동종료={(hud != null && hud.행동종료 != null ? hud.행동종료.interactable.ToString() : "없음")}"
                   + $" · 내손패={e.S.P.Hand.Count} 봇손패={e.S.B.Hand.Count} 고른={_dir.손패?.고른번호}"
                   + $" · 소켓회로={(_dir.소켓 != null && _dir.소켓.회로 != null ? _dir.소켓.회로.activeInHierarchy.ToString() : "없음")}"
                   + $" · 명판={(_dir.명판 != null && _dir.명판.오버레이 != null ? _dir.명판.오버레이.activeInHierarchy.ToString() : "없음")}");
                찍기("99_멈춘자리");
                마침($"판이 안 끝났다 · 결정 {결정}회 · {Time.realtimeSinceStartup - 시작:0.0}초"
                     + $" · {e.S.Turn}턴 {e.S.Step}");
                yield break;
            }

            yield return null;
            yield return null;
            찍기("09_종료화면");
            줄($"끝 · {e.S.Turn}턴 · {e.S.Result} · 서버 나 {e.S.P.AliveServers} 봇 {e.S.B.AliveServers}");
            줄($"종료화면 켜짐 = {(_flow.종료화면 != null && _flow.종료화면.activeInHierarchy)}");
            줄($"연출 확인 · 끌기 {(_찍은끌기 ? "O" : "X")} · 명판 {_본명판}회 · 칩 꽂힘 {_본칩}회 " +
               $"· 회로 {_본회로}회 · 적 배치 예고 {_본예고}회 · 발동 {(_찍은발동 ? "O" : "X")}");
            마침(점검());
        }

        string 점검()
        {
            if (연출생략) return "정상 종료 (연출 생략)";
            if (!눌러서낸다 && !_찍은끌기) return "끌어서 내는 길을 한 번도 못 지나갔다";
            if (_본명판 == 0) return "턴 전환 명판이 한 번도 안 떴다";
            if (_본칩 == 0) return "칩이 소켓에 한 번도 안 꽂혔다";
            if (_본회로 == 0) return "기판 회로가 한 번도 안 그어졌다";
            if (_본예고 == 0) return "적 배치 예고가 한 번도 안 떴다";
            return 눌러서낸다 ? "정상 종료 (눌러서 내기)" : "정상 종료";
        }

        /// <summary>
        /// 카드 한 장을 **사람과 똑같이 끌어서** 낸다.
        /// 집으면 놓을 자리가 켜지므로, 그 자리의 화면 좌표로 끌어다 놓는다.
        /// </summary>
        IEnumerator 한장내기(System.Action<bool> 결과)
        {
            결과(false);
            var e = _dir.Engine;
            var hand = _dir.손패;
            var board = _dir.판;
            if (hand?.카드들 == null || board == null) yield break;

            for (int i = 0; i < hand.카드들.Count; i++)
            {
                var cv = hand.카드들[i];
                if (cv == null || !cv.gameObject.activeSelf) continue;
                if (cv.버튼 == null || !cv.버튼.interactable) continue;

                int 전 = e.S.P.Hand.Count;
                var need = Rules.Of(cv.Card).Need;

                if (눌러서낸다)
                {
                    if (누르기로내기(cv, need, 전)) { 결과(true); yield break; }
                    continue;
                }

                // ① 집는다 — 이 순간 놓을 수 있는 자리가 켜진다
                var ped = 사건(화면점((RectTransform)cv.transform));
                cv.OnBeginDrag(ped);
                if (!cv.끌고있다) continue;
                _찍은끌기 = true;
                if (!_찍은칩 && !연출생략) 찍기("03_집은카드");

                // ② 놓을 자리로 끌고 간다
                var 자리 = 놓을자리(board, need);
                if (자리 == null)
                {
                    // 놓을 곳이 없다 — 판 밖에 떼어 취소되는지도 같이 확인한다
                    ped.position = new Vector2(20f, 20f);
                    cv.OnDrag(ped);
                    cv.OnEndDrag(ped);
                    if (e.S.P.Hand.Count != 전) { 줄("판 밖에 놓았는데 카드가 나갔다"); }
                    continue;
                }

                ped.position = 화면점(자리);
                cv.OnDrag(ped);
                yield return null;
                cv.OnEndDrag(ped);

                if (e.S.P.Hand.Count != 전)
                {
                    // 칩 소켓과 기판 회로 연출이 끝날 때까지 기다린다
                    while (_dir.연출중) yield return null;
                    결과(true);
                    yield break;
                }
            }
        }

        /// <summary>놓을 자리 하나. 켜져 있는 것(= 놓을 수 있는 것) 중에서 고른다.</summary>
        RectTransform 놓을자리(BoardView board, Rules.Target need)
        {
            if (need == Rules.Target.None) return board.판영역;
            var 것들 = need == Rules.Target.Lane ? board.채널버튼 : board.자리버튼;
            foreach (var b in 것들)
                if (b != null && b.gameObject.activeSelf && b.interactable)
                    return (RectTransform)b.transform;
            return null;
        }

        bool 누르기로내기(CardView cv, Rules.Target need, int 전)
        {
            var e = _dir.Engine;
            var board = _dir.판;
            cv.버튼.onClick.Invoke();
            if (need == Rules.Target.None) return e.S.P.Hand.Count != 전;

            var 자리 = need == Rules.Target.Lane ? board.채널버튼 : board.자리버튼;
            foreach (var b in 자리)
            {
                if (b == null || !b.gameObject.activeSelf || !b.interactable) continue;
                b.onClick.Invoke();
                if (e.S.P.Hand.Count != 전) return true;
            }
            return false;
        }

        /// <summary>
        /// 손가락 하나치 사건. `pressEventCamera` 는 읽기 전용이라 넣을 수 없다 —
        /// 받는 쪽이 비어 있으면 캔버스 카메라를 쓴다.
        /// </summary>
        PointerEventData 사건(Vector2 p) => new PointerEventData(EventSystem.current)
        {
            position = p,
            button = PointerEventData.InputButton.Left,
        };

        Vector2 화면점(RectTransform rt)
            => RectTransformUtility.WorldToScreenPoint(_cam, rt.TransformPoint(rt.rect.center));

        void 턴넘기기()
        {
            var hud = FindFirstObjectByType<HudView>();
            if (hud != null && hud.행동종료 != null && hud.행동종료.interactable)
                hud.행동종료.onClick.Invoke();
        }

        /// <summary>
        /// 연출이 실제로 화면에 떴는지 지켜본다. 숫자만 맞고 연출이 안 돌면
        /// "돌았다"고 보고하는 셈이 되므로, 본 횟수를 세고 처음 한 번은 사진으로 남긴다.
        /// </summary>
        IEnumerator 연출지켜보기()
        {
            var e = _dir.Engine;
            while (e != null && !e.S.Ended)
            {
                var 소켓 = _dir.소켓;
                var 명판 = _dir.명판;

                if (명판 != null && 명판.오버레이 != null && 명판.오버레이.activeInHierarchy)
                {
                    _본명판++;
                    // 동시 발동 명판만 화면 전체를 덮는다. 그 한 장도 따로 남긴다
                    bool 전체 = 명판.가림막 != null
                                && ((RectTransform)명판.가림막.transform).sizeDelta.x > 1000f;
                    if (전체 && !_찍은전체명판)
                    {
                        _찍은전체명판 = true;
                        yield return new WaitForSeconds(0.3f);
                        찍기("04_전환명판_동시발동");
                    }
                    else if (!전체 && !_찍은명판)
                    {
                        _찍은명판 = true;
                        yield return new WaitForSeconds(0.3f);
                        찍기("05_전환명판");
                    }
                    while (명판.오버레이.activeInHierarchy) yield return null;
                }
                if (_dir.손패 != null && _dir.손패.예고중)
                {
                    _본예고++;
                    if (!_찍은예고)
                    {
                        _찍은예고 = true;
                        yield return new WaitForSeconds(0.2f);
                        찍기("03b_적배치예고");
                    }
                    while (_dir.손패 != null && _dir.손패.예고중) yield return null;
                }
                if (소켓 != null && 소켓.회로 != null && 소켓.회로.activeInHierarchy)
                {
                    _본회로++;
                    // 첫 프레임에는 가닥이 아직 하나도 안 그어져 있다. 한가운데를 찍는다
                    if (!_찍은회로)
                    {
                        _찍은회로 = true;
                        yield return new WaitForSeconds(0.45f);
                        찍기("07_회로흡수");
                    }
                    while (소켓.회로 != null && 소켓.회로.activeInHierarchy) yield return null;
                }
                else if (소켓 != null && 소켓.리테이너번짐 != null && 소켓.리테이너번짐.enabled)
                {
                    _본칩++;
                    if (!_찍은칩) { _찍은칩 = true; 찍기("06_칩꽂힘"); }
                    while (소켓.리테이너번짐 != null && 소켓.리테이너번짐.enabled
                           && (소켓.회로 == null || !소켓.회로.activeInHierarchy)) yield return null;
                }
                if (!_찍은발동 && e.S.Step == Step.Resolve && _dir.연출중 && e.S.Fx.Count > 0)
                {
                    _찍은발동 = true;
                    // 빛줄기가 경로 한복판에 있을 때를 잡는다
                    yield return new WaitForSeconds(0.22f);
                    찍기("08_동시발동_발사");
                    yield return new WaitForSeconds(0.38f);
                    찍기("08_동시발동_착탄");
                }
                yield return null;
            }
        }

        void 줄(string s) { _log.AppendLine(s); Debug.Log("[자동] " + s); }

        void 마침(string why)
        {
            줄("결과: " + why);
            보고 = _log.ToString();
            Directory.CreateDirectory(사진폴더);
            File.WriteAllText(Path.Combine(사진폴더, "보고.txt"), 보고, Encoding.UTF8);
            if (!why.StartsWith("정상")) Debug.LogError("[자동] " + why);
            끝났다 = true;
        }

        void 찍기(string name)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;

            Directory.CreateDirectory(사진폴더);
            File.WriteAllBytes(Path.Combine(사진폴더, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Destroy(rt);
        }
    }
}
