using System.IO;
using CW.Core.Timeline.Serialization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Timeline;
using UnityEngine;

namespace CW.Core.Timeline.Editor
{
    public partial class TimelineVisualizer : EditorWindow
    {
        internal const float TIME_AREA_HEIGHT = 25f;
        internal const float TIME_AREA_MIN_WIDTH = 50f;
        internal const float MAX_TIME_AREA_SCALING = 9000f;
        internal const float MENU_HEIGHT = 30f;

        private GlobalTimeline _timeline;
        private ITimelineStyles _styles;

        bool _initialized;
        
        readonly CwControl _manipulators = new CwControl();

        CwTimelineWindowState _state;
        TimeArea _timeArea;

        private TreeViewState _treeviewState;
        internal CwTimelineTreeViewDataSource TreeViewData { get; private set; }
        CwTimelineTreeView _treeView;
        TreeViewController _controller;

        internal CwTimelineTreeViewDataSource TreeViewData2 { get; private set; }
        CwTimelineTreeView _treeView2;
        TreeViewController _controller2;

        float _horizontalScrollBarSize;
        float _verticalScrollBarSize;
        
        internal TimeArea TimeArea => _timeArea;
        public CwTimelineWindowState State => _state;

        public Rect ActivitiesRect => new Rect(0, TimeAreaRect.yMax, position.width, position.height - TimeAreaRect.yMax - _horizontalScrollBarSize);

        private TimelineSelection _selection;

        private bool _isDebug;

        private string _searchText;

        internal ITimelineStyles Styles => _styles;

        public void SetTimeline(GlobalTimeline timeline)
        {
            _timeline = timeline;
        }

        public void SetStyles(ITimelineStyles styles)
        {
            _styles = styles;
        }

        public void OnGUI()
        {
            //var rawType = Event.current.rawType; // TODO: rawType seems to be broken after calling Use(), use this Hack and remove it once it's fixed.
            var mousePosition = Event.current.mousePosition; // mousePosition is also affected by this bug and does not reflect the original position after a Use()

            InitializeGUIIfRequired();
            
            UpdateGUIConstants();

            var processManipulators = Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout;
            if (processManipulators)
            {
                // Update what's under mouse the cursor
                CwPicker.DoPick(_state, mousePosition);
                _manipulators.HandleManipulatorsEvents(_state);
                _state.SpacePartitioner.Clear();
            }
            
            DrawHeaderBackground();
            
            TimeArea.DrawMajorTicks(ActivitiesRect, 60f);

            MenuGUI();
            TimelineGUI();
            ActivitiesGUI();

            // if (_isDebug)
            // {
            //     _state.SpacePartitioner.DebugDraw();    
            // }
        }

        private void MenuGUI()
        {
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("save timeline", new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
                new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f)))
            {
                string path = EditorUtility.SaveFilePanel("Save timeline to json", "", "timeline", "json");
                if (path.Length != 0)
                {
                    var serializer = TimelineJsonSerializator.Create();
                    var str = serializer.Serialize(_timeline);
                    File.WriteAllText(path, str);
                }
            }
            
            if (GUILayout.Button("load timeline", new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
                new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f)))
            {
                string path = EditorUtility.OpenFilePanel("Open timeline json", "", "json");
                if (path.Length != 0)
                {
                    var fileContent = File.ReadAllText(path);
                    var serializer = TimelineJsonSerializator.Create();
                    _timeline =  serializer.Deserialize<GlobalTimeline>(fileContent);
                    var data = _timeline.BuildVisualization();
                    TreeViewData.SetData(data);
                    ReinitializeOneTree();
                }
            }

            if (GUILayout.Button("merge timeline", new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
                new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f)))
            {
                string path = EditorUtility.OpenFilePanel("Open timeline json", "", "json");
                if (path.Length != 0)
                {
                    var fileContent = File.ReadAllText(path);
                    var serializer = TimelineJsonSerializator.Create();
                    var tl =  serializer.Deserialize<GlobalTimeline>(fileContent);
                    var data = tl.BuildVisualization();
                    InitializeSecondActivityTree(data);
                }
            }

            GUILayout.Label("Search:", GUILayout.Width(50));
            _searchText = GUILayout.TextField(_searchText, GUILayout.Width(150));
            if (GUILayout.Button("Find", GUILayout.Width(40)))
            {
                var selection = _selection.GetSelection();
                var curId = selection?.id ?? 0;
                var foundId = TreeViewData.FindNextTerm(_searchText, curId);
                if (foundId != 0)
                {
                                        
                    var item = (ActivityTreeViewItem)TreeViewData.FindItem(foundId);
                    _selection.Select(item);
                    _controller.SetSelection(new[] { foundId }, true);
                }
            }

            // var oldIsDebug = _isDebug;
            // _isDebug = GUILayout.Toggle(_isDebug, "debug",
            //     new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
            //     new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f));
            //
            // if (oldIsDebug != _isDebug)
            // {
            //     SnapEngine.displayDebugLayout = _isDebug;
            //     if (_isDebug)
            //         Selection.activeObject = DirectorStyles.Instance.customSkin;
            // }

            if (TreeViewData2 != null)
            {
                TreeViewData2.TheSettings.ShiftX = GUILayout.HorizontalSlider(TreeViewData2.TheSettings.ShiftX, -30, 30,
                    new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
                    new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f));

                TreeViewData2.TheSettings.ShiftX = EditorGUILayout.FloatField("", TreeViewData2.TheSettings.ShiftX,
                    new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT - 5),
                    new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 200f));

                var old = TreeViewData2.TheSettings.ShiftY;
                TreeViewData2.TheSettings.ShiftY = GUILayout.HorizontalSlider(TreeViewData2.TheSettings.ShiftY, -3000, 3000,
                    new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT),
                    new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 100f));

                TreeViewData2.TheSettings.ShiftY = EditorGUILayout.FloatField("", TreeViewData2.TheSettings.ShiftY,
                    new GUILayoutOption(GUILayoutOption.Type.fixedHeight, MENU_HEIGHT - 5),
                    new GUILayoutOption(GUILayoutOption.Type.fixedWidth, 200f));

                if (old != TreeViewData2.TheSettings.ShiftY)
                {
                    _treeView2.CalculateRowRects();
                }
            }


            GUILayout.EndHorizontal();
        }

        void ActivitiesGUI()
        {
            var clientRect = ActivitiesRect;
            GUILayout.BeginVertical(GUILayout.Height(clientRect.height));
            if (_controller != null)
            {
                int keyboardControl = GUIUtility.GetControlID(FocusType.Passive, clientRect);
                _controller.OnGUI(clientRect, keyboardControl);
            }
            
            if (_controller2 != null)
            {
                int keyboardControl = GUIUtility.GetControlID(FocusType.Passive, clientRect);
                _controller2.OnGUI(clientRect, keyboardControl);
            }

            
            GUILayout.EndVertical();
        }

        void InitializeGUIIfRequired()
        {
            if (_initialized)
                return;
            
            _state = new CwTimelineWindowState(this);

            InitializeActivityTree();
            InitializeTimeArea();
            
            _selection = new TimelineSelection(_state);
            
            InitializeManipulators();

            _initialized = true;
        }

        void UpdateGUIConstants()
        {
            _horizontalScrollBarSize =
                GUI.skin.horizontalScrollbar.fixedHeight + GUI.skin.horizontalScrollbar.margin.top;
            _verticalScrollBarSize = (_controller != null && _controller.showingVerticalScrollBar)
                ? GUI.skin.verticalScrollbar.fixedWidth + GUI.skin.verticalScrollbar.margin.left
                : 0;
        }

        void InitializeActivityTree()
        {
            _treeviewState = new TreeViewState();
            _treeviewState.scrollPos = new Vector2(_treeviewState.scrollPos.x, 0);

            _controller = new TreeViewController(this, _treeviewState);
            _controller.horizontalScrollbarStyle = GUIStyle.none;
            //_controller.keyboardInputCallback = TreeViewKeyboardCallback;
            
            _treeView = new CwTimelineTreeView(_controller, _state);
            var timeline = _timeline;
            var visualData = timeline.BuildVisualization();

            TreeViewData = new CwTimelineTreeViewDataSource(_controller);    
            TreeViewData.SetData(visualData);
            TreeViewData.onVisibleRowsChanged += _treeView.CalculateRowRects;
            _controller.Init(position, TreeViewData, _treeView, null);

            TreeViewData.ExpandItems(TreeViewData.root);
        }

        void InitializeSecondActivityTree(GlobalTimelineVisualizationData data)
        {
            _controller2 = new TreeViewController(this, _treeviewState);
            _controller2.horizontalScrollbarStyle = GUIStyle.none;
            _controller2.useScrollView = true;
            _controller2.scrollViewStyle = GUIStyle.none;

            _treeView2 = new CwTimelineTreeView(_controller2, _state);
            TreeViewData2 = new CwTimelineTreeViewDataSource(_controller2);
            TreeViewData2.SetData(data);
            TreeViewData.TheSettings.Tint = new Color(0f, 0f, 1f, 0.47f);
            TreeViewData2.TheSettings.Tint = new Color(1f, 0f, 0f, 0.49f);
            TreeViewData2.onVisibleRowsChanged += _treeView2.CalculateRowRects;
            _controller2.Init(position, TreeViewData2, _treeView2, null);
            TreeViewData2.ExpandItems(TreeViewData2.root);
        }

        private void ReinitializeOneTree()
        {
            _treeView2 = null;
            TreeViewData2 = null;
            _controller2 = null;
            
            TreeViewData.ExpandItems(TreeViewData.root);
            var rangeMin = Mathf.Max(0f,TreeViewData.Start - 1f);
            var rangeMax = TreeViewData.End + 1f;
            TimeArea.hBaseRangeMin = rangeMin;
            TimeArea.hBaseRangeMax = rangeMax;
            TimeArea.SetShownHRange(rangeMin, rangeMax);
            
            Repaint();
        }


        void InitializeManipulators()
        {
            _manipulators.AddManipulator(new SelectManipulator(_selection));
        }

        void DrawHeaderBackground()
        {
            var rect = TimeAreaRect;
            EditorGUI.DrawRect(rect, DirectorStyles.Instance.customSkin.colorTimelineBackground);
        }
    }
}