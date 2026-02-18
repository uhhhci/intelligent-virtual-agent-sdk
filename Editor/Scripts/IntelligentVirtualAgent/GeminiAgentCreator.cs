using UnityEngine;

using UnityEditor;

using UnityEngine.UI;

using IVH.Core.IntelligentVirtualAgent;

using IVH.Core.ServiceConnector.Gemini.Realtime;



public class GeminiAgentCreator : Editor

{

    [MenuItem("IVH/Create Voice-Only Agent (No Avatar)")]

    public static void CreateVoiceAgent()

    {

        // 1. Create Agent Logic

        GameObject agentObj = new GameObject("Gemini_Voice_Agent");

        Undo.RegisterCreatedObjectUndo(agentObj, "Create Voice Agent");

        agentObj.AddComponent<GeminiRealtimeWrapper>();

        var audioSource = agentObj.AddComponent<AudioSource>();

        var agent = agentObj.AddComponent<GeminiVoiceOnlyAgent>();



        audioSource.playOnAwake = false;

        audioSource.spatialBlend = 0f; // 2D Sound



        // 2. Setup Canvas (Scale with Screen Size)

        GameObject canvasObj = new GameObject("Agent_UI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasObj.GetComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

       

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = new Vector2(1920, 1080);

        scaler.matchWidthOrHeight = 0.5f;



        // 3. Setup Log Panel (Bottom 35%)

        GameObject panelObj = new GameObject("Log_Panel", typeof(RectTransform), typeof(Image));

        panelObj.transform.SetParent(canvasObj.transform, false);

       

        Image panelImg = panelObj.GetComponent<Image>();

        panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f); // Darker background

       

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();

        panelRect.anchorMin = new Vector2(0.1f, 0.05f); // 10% padding from sides

        panelRect.anchorMax = new Vector2(0.9f, 0.35f);

        panelRect.offsetMin = Vector2.zero;

        panelRect.offsetMax = Vector2.zero;



        // 4. Scroll View

        GameObject scrollObj = new GameObject("Scroll_View", typeof(RectTransform), typeof(ScrollRect));

        scrollObj.transform.SetParent(panelObj.transform, false);

       

        ScrollRect scrollRect = scrollObj.GetComponent<ScrollRect>();

        RectTransform scrollTrans = scrollObj.GetComponent<RectTransform>();

        scrollTrans.anchorMin = Vector2.zero;

        scrollTrans.anchorMax = Vector2.one;

        scrollTrans.offsetMin = new Vector2(20, 20); // Padding inside panel

        scrollTrans.offsetMax = new Vector2(-20, -20);



        // 5. Viewport (Mask)

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));

        viewport.transform.SetParent(scrollObj.transform, false);

       

        RectTransform viewRect = viewport.GetComponent<RectTransform>();

        viewRect.anchorMin = Vector2.zero;

        viewRect.anchorMax = Vector2.one;

        viewRect.pivot = new Vector2(0, 1);

        viewRect.offsetMin = Vector2.zero;

        viewRect.offsetMax = Vector2.zero;

       

        viewport.GetComponent<Mask>().showMaskGraphic = false;

        viewport.GetComponent<Image>().color = new Color(1,1,1,0.01f);



        // 6. Content Container

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));

        content.transform.SetParent(viewport.transform, false);

       

        RectTransform contentRect = content.GetComponent<RectTransform>();

        contentRect.anchorMin = new Vector2(0, 1);

        contentRect.anchorMax = new Vector2(1, 1);

        contentRect.pivot = new Vector2(0.5f, 1);



        VerticalLayoutGroup vlg = content.GetComponent<VerticalLayoutGroup>();

        vlg.childControlHeight = true;

        vlg.childControlWidth = true;

        vlg.childForceExpandHeight = false;

        vlg.childForceExpandWidth = true;

        vlg.spacing = 10;

        vlg.padding = new RectOffset(10, 10, 10, 10); // Padding for text



        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();

        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;



        scrollRect.content = contentRect;

        scrollRect.viewport = viewRect;

        scrollRect.horizontal = false;

        scrollRect.movementType = ScrollRect.MovementType.Elastic;



        // 7. Create Text Object

        GameObject textObj = new GameObject("Log_Text", typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        textObj.transform.SetParent(content.transform, false);
        
        Text textComp = textObj.GetComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        textComp.font = font;        textComp.color = Color.white;
        textComp.fontSize = 28; 
        textComp.alignment = TextAnchor.UpperLeft;
        textComp.horizontalOverflow = HorizontalWrapMode.Wrap; 
        
        textComp.verticalOverflow = VerticalWrapMode.Overflow; 
        
        ContentSizeFitter textCsf = textObj.GetComponent<ContentSizeFitter>();
        textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;



        // 8. Link to Agent

        agent.logTextDisplay = textComp;

        agent.scrollRect = scrollRect;



        Selection.activeGameObject = agentObj;

        Debug.Log("Gemini Voice Agent UI Re-Created Successfully.");

    }

}