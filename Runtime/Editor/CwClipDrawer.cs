using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using Graphics = UnityEditor.Timeline.Graphics;

namespace CW.Core.Timeline.Editor
{ 
    
    public class ClipBorder
    {
        public readonly Color color;
        public readonly float thickness;

        ClipBorder(Color color, float thickness)
        {
            this.color = color;
            this.thickness = thickness;
        }

        const float k_ClipSelectionBorder = 1.0f;
        const float k_ClipRecordingBorder = 2.0f;

        public static readonly ClipBorder kSelection = new ClipBorder(Color.white, k_ClipSelectionBorder);
        public static readonly ClipBorder kSelectionParent = new ClipBorder(new Color(1f, 0f, 0.89f), k_ClipSelectionBorder);
        public static readonly ClipBorder kSelectionChildren = new ClipBorder(new Color(1f, 0.72f, 0f), k_ClipSelectionBorder);
        public static readonly ClipBorder kRecording = new ClipBorder(DirectorStyles.Instance.customSkin.colorRecordingClipOutline, k_ClipRecordingBorder);
    }
    
    public class CwClipDrawer
    {
        const float k_ClipLabelPadding = 6.0f;
        const float k_ClipLabelMinWidth = 10.0f;
        const float k_IconsPadding = 1.0f;
        const float k_ClipInlineWidth = 2.0f;
        
        static readonly Color s_InlineLightColor = new Color(1.0f, 1.0f, 1.0f, 0.2f);
        static readonly Color s_InlineShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.2f);

        static readonly GUIContent s_TitleContent = new GUIContent();
        private static Texture s_arrowHead;

        public static void DrawSimpleClip(CwClipDrawData drawData, bool isReference)
        {
            GUI.BeginClip(drawData.rect);

            var clipRect = new Rect(0.0f, 0.0f, drawData.rect.width, drawData.rect.height);

            var orgColor = GUI.color;
            var color = drawData.highlightColor;
            if (isReference)
                color.a /= 4f;
            GUI.color = color;

            DrawClipBackground(clipRect, drawData.selectionState, drawData.alpha);
            GUI.color = orgColor;

            var shadowColor = s_InlineShadowColor * color;
            var lightColor = s_InlineLightColor * color;
            DrawClipEdges(clipRect, lightColor, shadowColor, drawData.leftEdgeIsOpen ? Color.red : lightColor, drawData.rightEdgeIsOpen ? Color.red : shadowColor);

            var textRect = clipRect;

            textRect.xMin += k_ClipLabelPadding;
            textRect.xMax -= k_ClipLabelPadding;

            if (textRect.width > k_ClipLabelMinWidth)
            {
                var textColor = Color.white;
                if (isReference)
                    textColor.a /= 2f;
                DrawClipLabel(drawData.title, textRect, textColor);
            }

            if (drawData.selectionState != SelectionState.None)
            {
                DrawBorder(clipRect, drawData.selectionState switch
                {
                    SelectionState.Selected => ClipBorder.kSelection,
                    SelectionState.ChildSelected => ClipBorder.kSelectionParent,
                    SelectionState.ParentSelected => ClipBorder.kSelectionChildren
                });
            }

            GUI.EndClip();
        }

        public static void DrawReference(Rect rect)
        {
            GUI.BeginClip(rect);
            var r = new Rect(0, 0, rect.width, rect.height);
            
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1f, r.height), Color.white);

            if(s_arrowHead == null)
                s_arrowHead = Resources.Load<Texture>("Gui/arrowhead");
            Vector2 @from = new Vector2(r.xMin, r.center.y);
            var size = 16;
            GUI.Label(new Rect(@from.x, @from.y - size/2, size, size), s_arrowHead);
            
            EditorGUI.DrawRect(new Rect(@from.x + size, @from.y - 1f, r.width - size, 1f), new Color(1f, 1f, 1f, 0.5f));
            
            GUI.EndClip();
        }

        static void DrawClipEdges(Rect targetRect, Color lightColor, Color shadowColor, Color leftEdgeColor,
            Color rightEdgeColor)
        {
            // Draw Colored Line at the bottom.
//            var colorRect = targetRect;
//            colorRect.yMin = colorRect.yMax - k_ClipSwatchLineThickness;
//
//            EditorGUI.DrawRect(colorRect, swatchColor);

            // Draw Highlighted line at the top
            EditorGUI.DrawRect(
                new Rect(targetRect.xMin, targetRect.yMin, targetRect.width - k_ClipInlineWidth, k_ClipInlineWidth),
                lightColor);

            // Draw Highlighted line at the left
            EditorGUI.DrawRect(
                new Rect(targetRect.xMin, targetRect.yMin + k_ClipInlineWidth, k_ClipInlineWidth,
                    targetRect.height),
                leftEdgeColor);

            // Draw darker vertical line at the right of the clip
            EditorGUI.DrawRect(
                new Rect(targetRect.xMax - k_ClipInlineWidth, targetRect.yMin, k_ClipInlineWidth,
                    targetRect.height),
                rightEdgeColor);

            // Draw darker vertical line at the bottom of the clip
            EditorGUI.DrawRect(
                new Rect(targetRect.xMin, targetRect.yMax - k_ClipInlineWidth, targetRect.width, k_ClipInlineWidth),
                shadowColor);
        }

        private static void DrawBorder(Rect centerRect, ClipBorder border)
        {
            var thickness = border.thickness;
            var color = border.color;

            // Draw top selected lines.
            EditorGUI.DrawRect(new Rect(centerRect.xMin, centerRect.yMin, centerRect.width, thickness), color);

            // Draw bottom selected lines.
            EditorGUI.DrawRect(new Rect(centerRect.xMin, centerRect.yMax - thickness, centerRect.width, thickness), color);

            // Draw Left Selected Lines
            EditorGUI.DrawRect(new Rect(centerRect.xMin, centerRect.yMin, thickness, centerRect.height), color);
           

            // Draw Right Selected Lines
            EditorGUI.DrawRect(new Rect(centerRect.xMax - thickness, centerRect.yMin, thickness, centerRect.height), color);
        }
        
        static void DrawClipLabel(string title, Rect availableRect, Color color)
        {
            var textColor = color;

            DrawClipLabel(title, availableRect, textColor, title);
        }
        
        static void DrawClipLabel(string title, Rect availableRect, Color textColor, string tooltipMessage = "")
        {
            s_TitleContent.text = title;
            var neededTextWidth = DirectorStyles.Instance.fontClip.CalcSize(s_TitleContent).x;
            var neededIconWidthLeft = 0.0f;
            var neededIconWidthRight = 0.0f;

            if (neededTextWidth > availableRect.width)
                s_TitleContent.text = DirectorStyles.Elipsify(title, availableRect.width, neededTextWidth);

            s_TitleContent.tooltip = tooltipMessage;
            DrawClipName(availableRect, s_TitleContent, textColor);
        }
        
        static void DrawClipName(Rect rect, GUIContent content, Color textColor)
        {
            Graphics.ShadowLabel(rect, content, DirectorStyles.Instance.fontClip, textColor, Color.black);
        }


//        private static GUIStyle s_test;
        static void DrawClipBackground(Rect clipCenterSection, SelectionState selected, float alpha)
        {
            var clipStyle = selected == SelectionState.Selected ? DirectorStyles.Instance.customSkin.clipSelectedBckg : DirectorStyles.Instance.customSkin.clipBckg;
            if (alpha < 1f)
                clipStyle = DirectorStyles.Instance.customSkin.colorLockTextBG;

//            if (s_test == null)
//            {
//                s_test = Colorize(DirectorStyles.Instance.timelineClip, new Color(1f, 0.96f, 0.45f, 0.67f));
//            }

            EditorGUI.DrawRect(clipCenterSection, clipStyle);
        }

        static GUIStyle Colorize(GUIStyle src, Color color)
        {
            var style = new GUIStyle(src);
            
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0,0, color);
            texture.Apply();
            style.normal.background = texture;
            
            return style;
        }
    }
}