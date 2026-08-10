using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using IVH.Core.IntelligentVirtualAgent;
using IVH.Core.ServiceConnector.Gemini.Realtime;

public class GeminiAgentCreator : Editor
{
    [MenuItem("IVA SDK/Create Voice-Only Agent (No Avatar)")]
    public static void CreateVoiceAgent()
    {
        GameObject agentObj = new GameObject("Gemini_Voice_Agent");
        Undo.RegisterCreatedObjectUndo(agentObj, "Create Voice Agent");
        agentObj.AddComponent<GeminiRealtimeWrapper>();
        var audioSource = agentObj.AddComponent<AudioSource>();
        var agent = agentObj.AddComponent<GeminiVoiceOnlyAgent>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        GameObject canvasObj = new GameObject("Agent_UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObj = new GameObject("Log_Panel", typeof(RectTransform), typeof(Image));
        panelObj.transform.SetParent(canvasObj.transform, false);

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.05f);
        panelRect.anchorMax = new Vector2(0.9f, 0.35f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // ScrollRect is added later (after Content/Viewport exist) to avoid premature initialization.
        GameObject scrollObj = new GameObject("Scroll_View", typeof(RectTransform));
        scrollObj.transform.SetParent(panelObj.transform, false);

        RectTransform scrollTrans = scrollObj.GetComponent<RectTransform>();
        scrollTrans.anchorMin = Vector2.zero;
        scrollTrans.anchorMax = Vector2.one;
        scrollTrans.offsetMin = new Vector2(20, 20);
        scrollTrans.offsetMax = new Vector2(-20, -20);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scrollObj.transform, false);

        RectTransform viewRect = viewport.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.pivot = new Vector2(0, 1);
        viewRect.offsetMin = Vector2.zero;
        viewRect.offsetMax = Vector2.zero;

        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewRect, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject textObj = new GameObject("Log_Text", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        textObj.transform.SetParent(contentRect, false);

        Text textComp = textObj.GetComponent<Text>();
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textComp.color = Color.white;
        textComp.fontSize = 24;
        textComp.alignment = TextAnchor.UpperLeft;
        textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComp.verticalOverflow = VerticalWrapMode.Truncate;

        ContentSizeFitter textCsf = textObj.GetComponent<Text>().gameObject.GetComponent<ContentSizeFitter>();
        textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewRect;
        scrollRect.vertical = true;
        scrollRect.horizontal = false;

        agent.logTextDisplay = textComp;
        agent.scrollRect = scrollRect;

        Selection.activeGameObject = agentObj;
        Debug.Log("Gemini Voice Agent UI Created Successfully.");
    }
}
