using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breach.UI
{
    public sealed class UiSequencePresentation : MonoBehaviour
    {
        sealed class DrawPart
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Start, End;
            public Vector3 StartScale, EndScale;
            public float Alpha, Delay;
            public bool Rays, Temporary;
        }
        sealed class HiddenBack { public CanvasGroup Group; public float Alpha; }
        BattleController c;
        readonly List<DrawPart> draws = new List<DrawPart>();
        readonly List<HiddenBack> hiddenBacks = new List<HiddenBack>();
        readonly List<RectTransform> resultParts = new List<RectTransform>();
        readonly List<Vector2> resultHomes = new List<Vector2>();
        CanvasGroup resultGroup;
        TMP_Text summary;
        RectTransform drawRoot;
        float Delta { get { return Time.unscaledDeltaTime * Mathf.Max(.1f, c.animationSpeed); } }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static CanvasGroup Group(GameObject item) { var group = item.GetComponent<CanvasGroup>(); return group ? group : item.AddComponent<CanvasGroup>(); }

        public void Initialize(BattleController controller)
        {
            c = controller;
            resultGroup = Group(c.result);
            summary = Ui.Text("확정 결과 요약", c.result.transform, 300, 645, 1320, 150, "", c.font, 19, new Color(.7f, .82f, .86f));
            summary.alignment = TextAlignmentOptions.Center;
            summary.textWrappingMode = TextWrappingModes.Normal;
            foreach (var rect in new[] { c.resultLabel.rectTransform, (RectTransform)c.retryButton.transform, summary.rectTransform })
            {
                resultParts.Add(rect); resultHomes.Add(rect.anchoredPosition);
            }
        }

        public void ShowTitle() { Cancel(); c.title.SetActive(true); c.titleScreen.Show(); }
        void RestoreTitle() { if(c && c.titleScreen) c.titleScreen.FinishIntro(); }

        RectTransform FindOnBoard(string name)
        {
            foreach (var background in c.backgrounds) if (background.activeSelf) return BattleController.Find(background.transform, name) as RectTransform;
            return null;
        }
        Vector2 DeckOrigin(Transform parent, RectTransform deck)
        {
            return deck ? (Vector2)parent.InverseTransformPoint(deck.position) : new Vector2(1537.75f, -388);
        }
        public IEnumerator Draw(IList<RectTransform> arrivals, int enemyAdded, int enemyTotal)
        {
            RestoreDraw();
            var deck = FindOnBoard("Deck_Back");
            float deckHeight = deck ? deck.rect.height : 188;
            int count = 0;
            for (int i = 0; i < arrivals.Count; i++)
            {
                var rect = arrivals[i]; if (!rect) continue;
                var group = Group(rect.gameObject);
                draws.Add(new DrawPart
                {
                    Rect = rect,
                    Group = group,
                    Start = DeckOrigin(rect.parent, deck),
                    End = rect.anchoredPosition,
                    StartScale = Vector3.one * (deckHeight / Mathf.Max(1, rect.rect.height)),
                    EndScale = rect.localScale,
                    Alpha = group.alpha,
                    Rays = group.blocksRaycasts,
                    Delay = i * .055f
                });
                group.alpha = 0; group.blocksRaycasts = false; count = Mathf.Max(count, i + 1);
            }
            if (enemyAdded > 0 && c.feedback && c.feedback.backImage)
            {
                drawRoot = Ui.Rect("공개된 손패 보충", c.fx, 0, 0, 1920, 1080);
                int first = Mathf.Max(0, enemyTotal - enemyAdded);
                for (int slot = first; slot < enemyTotal; slot++)
                {
                    var destination = FindOnBoard("Enemy_Back_" + (374 + slot)); if (!destination) continue;
                    foreach (var background in c.backgrounds)
                    {
                        var holder = BattleController.Find(background.transform, "Source_64_" + (374 + slot)); if (!holder) continue;
                        var holderGroup = Group(holder.gameObject); hiddenBacks.Add(new HiddenBack { Group = holderGroup, Alpha = holderGroup.alpha }); holderGroup.alpha = 0;
                    }
                    var rect = Ui.Rect("상대 드로우 - 뒷면", drawRoot, 0, 0, 117.5f, 188);
                    var image = rect.gameObject.AddComponent<Image>(); image.sprite = c.feedback.backImage; image.raycastTarget = false; image.preserveAspect = true;
                    var group = Group(rect.gameObject); group.alpha = 0;
                    draws.Add(new DrawPart
                    {
                        Rect = rect,
                        Group = group,
                        // Only my deck is visible; opponent replenishment stays inside its own hand panel.
                        Start = (Vector2)drawRoot.InverseTransformPoint(destination.position) + Vector2.down * 30,
                        End = (Vector2)drawRoot.InverseTransformPoint(destination.position),
                        StartScale = Vector3.one * (destination.rect.height / 188),
                        EndScale = Vector3.one * (destination.rect.height / 188),
                        Alpha = 1,
                        Delay = (slot - first) * .055f,
                        Temporary = true
                    });
                    count = Mathf.Max(count, slot - first + 1);
                }
            }
            if (count == 0) { RestoreDraw(); yield break; }
            float duration = .48f + (count - 1) * .055f;
            for (float elapsed = 0; elapsed < duration; elapsed += Delta)
            {
                foreach (var part in draws)
                {
                    if (!part.Rect) continue;
                    float t = Mathf.Clamp01((elapsed - part.Delay) / .48f), e = Ease(t);
                    part.Rect.anchoredPosition = Vector2.Lerp(part.Start, part.End, e) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * 55);
                    part.Rect.localScale = Vector3.Lerp(part.StartScale, part.EndScale, e);
                    part.Group.alpha = part.Alpha * Mathf.Clamp01(t / .15f);
                }
                yield return null;
            }
            RestoreDraw();
        }
        void RestoreDraw()
        {
            foreach (var part in draws) if (part.Rect && !part.Temporary)
            {
                part.Rect.anchoredPosition = part.End; part.Rect.localScale = part.EndScale;
                part.Group.alpha = part.Alpha; part.Group.blocksRaycasts = part.Rays;
            }
            draws.Clear();
            foreach (var back in hiddenBacks) if (back.Group) back.Group.alpha = back.Alpha;
            hiddenBacks.Clear();
            if (drawRoot) { drawRoot.gameObject.SetActive(false); Destroy(drawRoot.gameObject); drawRoot = null; }
        }

        public IEnumerator Result(Resolution report)
        {
            int mineBefore = 0, enemyBefore = 0;
            for (int lane = 0; lane < 3; lane++) { if (report.Before[0, lane] > 0) mineBefore++; if (report.Before[1, lane] > 0) enemyBefore++; }
            var events = report.Events.Where(e => !e.EndsWith(" 공개")).ToArray();
            string last = string.Join("  /  ", events.Skip(Mathf.Max(0, events.Length - 2)));
            summary.text = "ROUND " + c.State.Round + " · 최종 생존 서버  나 " + c.State.Alive(0) + " / 3  ·  상대 " + c.State.Alive(1) + " / 3"
             + "\n이번 해결 서버 파괴  나 " + (mineBefore - c.State.Alive(0)) + "  ·  상대 " + (enemyBefore - c.State.Alive(1))
             + (last.Length > 0 ? "\n" + last : "") + "\nENTER · 다시 시작    ESC · 타이틀";
            c.resultLabel.color = c.State.Winner == 0 ? Ui.Cyan : c.State.Winner == 1 ? Ui.Red : Ui.Gold;
            resultGroup.alpha = 0; c.retryButton.interactable = false; c.result.SetActive(true);
            for (float t = 0; t < 1; t += Delta / .42f)
            {
                float e = Ease(t); resultGroup.alpha = e;
                for (int i = 0; i < resultParts.Count; i++) if (resultParts[i]) resultParts[i].anchoredPosition = resultHomes[i] + new Vector2(0, -18) * (1 - e);
                yield return null;
            }
            RestoreResult();
        }
        void RestoreResult()
        {
            if (resultGroup) resultGroup.alpha = 1;
            for (int i = 0; i < resultParts.Count; i++) if (resultParts[i]) resultParts[i].anchoredPosition = resultHomes[i];
            if (c && c.retryButton) c.retryButton.interactable = true;
        }
        public void Cancel()
        {
            RestoreTitle(); RestoreDraw(); RestoreResult();
        }
        void OnDisable() { Cancel(); }
    }
}
