using DG.Tweening;
using K3Engine.Common;
using K3Engine.Component.Events;
using K3Engine.Component.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace K3Engine.Component
{
    [System.Serializable]
    [ExecuteInEditMode]
    public class K3Panel : K3Image, IK3Container, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, ICanvasRaycastFilter, InterfaceSerialize
    {
        #region  序列化字段
        [HideInInspector]
        [SerializeField]
        protected bool m_isTabPanel = false;
        [SerializeField]
        protected bool m_blockVisitNpc = true;
        [SerializeField]
        protected bool m_responseEsc = true;
        [SerializeField]
        protected uint m_closeSoundId;
        [SerializeField]
        protected uint m_openSoundId;
        [SerializeField]
        protected bool m_responsePress = false;
        [SerializeField]
        private ChangeSceneType m_changeSceneType = ChangeSceneType.destroy;
        [SerializeField]
        public bool useWindownDraw = false;
        [SerializeField]
        public Rect WindowRect = new Rect(0, 0, 0, 0);
        [SerializeField]
        public bool useWindowRay = false;
        [SerializeField]
        public RectTransform m_ClickRayTarget = null;
        [SerializeField]
        public bool needDraw = false;
        #endregion

        public static float sLongPressTime = 0.5f;
        private K3ClickEvent m_onClick = new K3ClickEvent();
        private K3ClickHandler _clickHandler;
        /// <summary>
        /// 0.5s判定为长按
        /// </summary>
        protected float m_longPressTime = 0.5f;
        [HotFixIgnore]
        public float longPressTime
        {
            get
            {
                return m_longPressTime;
            }
            set
            {
                m_longPressTime = value;
            }
        }

        /// <summary>
        /// 开始按下的时间
        /// </summary>
        protected float m_pressDownTime = 0f;
        [HotFixIgnore]
        public float pressDownTime
        {
            get
            {
                return m_pressDownTime;
            }
            set
            {
                m_pressDownTime = value;
            }
        }
        protected K3LongPressHandler m_longPressHandler;

        [HotFixIgnore]
        public K3ClickHandler clickHandler
        {
            set { _clickHandler = value; }
            get { return _clickHandler; }
        }

        private bool m_interavtive = true;
        [HotFixIgnore]
        public bool interavtive
        {
            set { m_interavtive = value; }
            get { return m_interavtive; }
        }
        [HotFixIgnore]
        public bool blockVisitNpc
        {
            get
            {
                return m_blockVisitNpc;
            }
            set
            {
                m_blockVisitNpc = value;
            }
        }
        [HotFixIgnore]
        public bool responseEsc
        {
            get
            {
                return m_responseEsc;
            }

            set
            {
                m_responseEsc = value;
            }
        }


        [HotFixIgnore]
        public uint closeSoundId
        {
            get { return m_closeSoundId; }
        }

        [HotFixIgnore]
        public uint openSoundId
        {
            get { return m_openSoundId; }
        }

        [HotFixIgnore]
        public bool responsePress
        {
            set { m_responsePress = value; }
            get { return m_responsePress; }
        }

        [HotFixIgnore]
        public bool resposePress
        {
            get { return m_responsePress; }
            set { m_responsePress = value; }
        }

        protected K3Panel()
        {

        }

        protected override void Start()
        {
            base.Start();
            m_longPressTime = sLongPressTime;
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            if (useWindownDraw)
            {
                List<UIVertex> vts = new List<UIVertex>();
                toFill.GetUIVertexStream(vts);
                toFill.Clear();

                UIVertex vert = UIVertex.simpleVert;
                vert.color = color;
                Vector2 selfPiovt = rectTransform.pivot;
                Rect selfRect = rectTransform.rect;
                float outerLx = -selfPiovt.x * selfRect.width;
                float outerBy = -selfPiovt.y * selfRect.height;
                float outerRx = (1 - selfPiovt.x) * selfRect.width;
                float outerTy = (1 - selfPiovt.y) * selfRect.height;

                vert.position = new Vector3(outerLx, outerTy);
                vert.uv0 = vts[0].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(outerRx, outerTy);
                vert.uv0 = vts[1].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(outerRx, outerBy);
                vert.uv0 = vts[2].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(outerLx, outerBy);
                vert.uv0 = vts[3].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(WindowRect.x, WindowRect.y - WindowRect.height);
                vert.uv0 = vts[0].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(WindowRect.x + WindowRect.width, WindowRect.y - WindowRect.height);
                vert.uv0 = vts[1].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(WindowRect.x + WindowRect.width, WindowRect.y);
                vert.uv0 = vts[2].uv0;
                toFill.AddVert(vert);
                vert.position = new Vector3(WindowRect.x, WindowRect.y);
                vert.uv0 = vts[3].uv0;
                toFill.AddVert(vert);
                toFill.AddTriangle(1, 7, 0);
                toFill.AddTriangle(1, 6, 7);
                toFill.AddTriangle(2, 6, 1);
                toFill.AddTriangle(2, 5, 6);
                toFill.AddTriangle(3, 5, 2);
                toFill.AddTriangle(3, 4, 5);
                toFill.AddTriangle(0, 4, 3);
                toFill.AddTriangle(0, 7, 4);

            }
        }
        
        public void SetWindow(RectTransform _target, float _x, float _y, float _w, float _h)
        {	
            if (useWindownDraw == true && WindowRect != null && WindowRect.x == _x && WindowRect.y == _y && WindowRect.width == _w && WindowRect.height == _h)
            {

            }
            else
            {
                useWindownDraw = true;
                WindowRect = new Rect(_x, _y, _w, _h);
                m_ClickRayTarget = _target;
                needDraw = true;
                SetAllDirty();
            }
        }

        bool ICanvasRaycastFilter.IsRaycastLocationValid(Vector2 pos, Camera eventCamera)
        {
            if (!useWindowRay || useWindownDraw == false || m_ClickRayTarget == null) return true;
            return !RectTransformUtility.RectangleContainsScreenPoint(m_ClickRayTarget, pos, eventCamera);
        }

        [HotFixIgnore]
        public bool isTabPanel
        {
            get
            {
                return m_isTabPanel;
            }
        }

        private bool m_raycastTargetAll;

        [HotFixIgnore]
        public bool raycastTargetAll
        {
            set { m_raycastTargetAll = value; }
            get { return m_raycastTargetAll; }
        }

        private Dictionary<int, Graphic> m_childrenGraphics;

        [HotFixIgnore]
        public Dictionary<int, Graphic> childrenGraphics
        {
            set { m_childrenGraphics = value; }
            get { return m_childrenGraphics; }
        }

        private Dictionary<int, Graphic> m_pointerDownGraphics;
        [HotFixIgnore]
        public Dictionary<int, Graphic> pointerDownGraphics
        {
            set { m_pointerDownGraphics = value; }
            get { return m_pointerDownGraphics; }
        }

        private Dictionary<int, Graphic> m_dragedGraphics;
        [HotFixIgnore]
        public Dictionary<int, Graphic> dragedGraphics
        {
            set { m_dragedGraphics = value; }
            get { return m_dragedGraphics; }
        }

        public void SetRaycastTargetAll(bool value)
        {
            m_raycastTargetAll = value;
            if (m_raycastTargetAll)
            {
                canvasGroup.blocksRaycasts = true;
                var graphics = this.recttransform.GetComponentsInChildren<Graphic>(true);
                m_childrenGraphics = new Dictionary<int, Graphic>();
                m_pointerDownGraphics = new Dictionary<int, Graphic>();
                m_dragedGraphics = new Dictionary<int, Graphic>();
                foreach (var graphic in graphics)
                {
                    if (graphic != this && graphic.raycastTarget && !m_childrenGraphics.ContainsKey(graphic.GetInstanceID()))
                    {
                        m_childrenGraphics.Add(graphic.GetInstanceID(), graphic);
                        graphic.raycastTarget = false;
                    }
                }
            }
            else
            {
                if (m_childrenGraphics != null)
                {
                    m_childrenGraphics.Clear();
                }
                if (m_pointerDownGraphics != null)
                {
                    m_pointerDownGraphics.Clear();
                }
            }
        }

        public void SetIsTabPanel(bool value)
        {
            m_isTabPanel = value;
        }

        public void SetClickable(bool value)
        {
            m_interavtive = value;
        }

        private void Press()
        {
            if (m_interavtive == false)
            {
                return;
            }
            m_onClick.Invoke();
        }

        [HotFixIgnore]
        public override float alpha
        {
            get
            {
                return canvasGroup.alpha;
            }
            set
            {
                if (canvasGroup.alpha != value)
                {
                    canvasGroup.alpha = value;
                }
            }
        }

        [HotFixIgnore]
        public override string ComponentName
        {
            get { return "K3Panel"; }
        }


        [HotFixIgnore]
        public K3ClickEvent onClick
        {
            get
            {
                return m_onClick;
            }
            set
            {
                m_onClick = value;
            }
        }

        public void DoScale(float value, float duration = 0.25f)
        {
            if (m_scaleTweener != null)
            {
                m_scaleTweener.Kill();
            }
            m_scaleTweener = recttransform.DOScale(value, duration);
        }

        public void MoveTo(Vector2 position, float duration = 0.25f, Action func = null)
        {
            if (m_moveTweener != null)
            {
                m_moveTweener.Kill();
            }
            m_moveTweener = rectTransform.DOAnchorPos(position, duration);
            if (func != null)
            {
                m_moveTweener.onComplete += () => func();
            }
        }



        private float m_shift2Pos = -100000;
        [HotFixIgnore]
        public float shift2Pos
        {
            get
            {
                return m_shift2Pos;
            }
        }

        private Vector3 m_oldScale = Vector3.one;
        [HotFixIgnore]
        public Vector3 oldScale
        {
            set { m_oldScale = value; }
            get { return m_oldScale; }
        }

        private PolygonCollider2D m_polygonCollider;
        [HotFixIgnore]
        public PolygonCollider2D polygonCollider
        {
            set { m_polygonCollider = value; }
            get { return m_polygonCollider; }
        }


        protected override void Awake()
        {
            base.Awake();
            m_oldScale = rectTransform.localScale;
            m_polygonCollider = rectTransform.GetComponent<PolygonCollider2D>();
        }

        public void MoveX(float x, float duration = 0.25f, Action func = null)
        {	
            if (m_moveTweener != null)
            {
                m_moveTweener.Kill();
            }

            float offset = 0.0f;
            if (SystemInfoUtil.Instance.isNotch)
            {
                if (SystemInfoUtil.Instance.CurScreenOrientation == ScreenOrientation.LandscapeLeft)
                {
                    if (property.screenAdaptType == ScreenAdaptType.TypePositive)
                    {
                        offset = SystemInfoUtil.Instance.NotchWidth;
                    }
                }
                else if (SystemInfoUtil.Instance.CurScreenOrientation == ScreenOrientation.LandscapeRight)
                {
                    if (property.screenAdaptType == ScreenAdaptType.TypeNegtive)
                    {
                        offset = (-SystemInfoUtil.Instance.NotchWidth);
                    }
                }
            }
            //else if(SystemInfoUtil.Instance.LeftRoundScreenOffset != 0 || SystemInfoUtil.Instance.RightRoundScreenOffset != 0)
            //{

            //    if (property.screenAdaptType == ScreenAdaptType.TypePositive)
            //    {
            //        offset = SystemInfoUtil.Instance.LeftRoundScreenOffset;

            //    }
            //    else
            //    {
            //        offset = -SystemInfoUtil.Instance.RightRoundScreenOffset;
            //    }
            //}

            m_moveTweener = rectTransform.DOAnchorPosX(x + offset, duration);
            m_shift2Pos = x;
            if (func != null)
            {
                m_moveTweener.onComplete += () => func();
            }
        }

        public void MoveY(float y, float duration = 0.25f, Action func = null)
        {
            if (m_moveTweener != null)
            {
                m_moveTweener.Kill();
            }
            m_moveTweener = rectTransform.DOAnchorPosY(y, duration);
            if (func != null)
            {
                m_moveTweener.onComplete += () => func();
            }
        }
        [HotFixIgnore]
        public GridLayoutGroup gridLayout
        {
            get
            {
                return getComponent<GridLayoutGroup>();
            }
        }
        [HotFixIgnore]
        public VerticalLayoutGroup verticalLayout
        {
            get
            {
                return getComponent<VerticalLayoutGroup>();
            }
        }
        [HotFixIgnore]
        public HorizontalLayoutGroup horizontalLayout
        {
            get
            {
                return getComponent<HorizontalLayoutGroup>();
            }
        }

        public void DoFadeTo(float endvalue, float duration, Action<int> func = null, int index = 0)
        {
            if (m_fadeTweener != null)
            {
                m_fadeTweener.Kill();
            }
            m_fadeTweener = canvasGroup.DOFade(endvalue, duration);
            if (func != null)
            {
                m_fadeTweener.OnComplete(delegate () { func(index); });
            }
        }

        public void AddEventListener(K3ClickHandler handler)
        {
            _clickHandler -= handler;
            _clickHandler += handler;
        }

        public void AddEventListenerForLua(K3ClickHandler handler)
        {
            _clickHandler -= handler;
            _clickHandler += handler;
        }

        public void AddLongPressHandler(K3LongPressHandler handler)
        {
            m_longPressHandler -= handler;
            m_longPressHandler += handler;
        }

        public void RemoveLongPressHandler(K3LongPressHandler handler)
        {
            m_longPressHandler -= handler;
        }

        public void RemoveEventListener(K3ClickHandler handler)
        {
            _clickHandler -= handler;
        }


        public void RemoveEventListener(UnityAction listener)
        {
            onClick.RemoveListener(listener);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }
            Press();
            if (_clickHandler != null)
            {
                if (panelEx != null)
                {
                    _clickHandler(K3ClickArgs.GetSharedClickArgs(eventData, panelEx, customData));
                    K3ClickArgs.Clear();
                }
                else
                {
                    _clickHandler(K3ClickArgs.GetSharedClickArgs(eventData, this, customData));
                    K3ClickArgs.Clear();
                }
            }

            if (m_raycastTargetAll)
            {
                foreach (var pair in m_pointerDownGraphics)
                {
                    IPointerClickHandler ipc = pair.Value.GetComponent<IPointerClickHandler>();
                    if (ipc != null)
                    {
                        ipc.OnPointerClick(eventData);
                    }
                }
            }
        }

        private K3PanelEx m_panelEx;
        protected K3PanelEx panelEx
        {
            get
            {
                return m_panelEx ?? (m_panelEx = this.gameObject.GetComponent<K3PanelEx>());
            }
        }


        public ChangeSceneType changeSceneType
        {
            get
            {
                return m_changeSceneType;
            }

            set
            {
                m_changeSceneType = value;
            }
        }

        public T getComponent<T>()
        {
            return gameObject.GetComponent<T>();
        }

        public T addComponent<T>()
            where T : MonoBehaviour
        {
            T newT = gameObject.AddComponent<T>();
            if ((newT as K3PanelEx) && (newT as K3PanelEx).getTargetContainer() == null)
            {
                (newT as K3PanelEx).LoadImmediate(gameObject);
            }
            return newT;
        }


        public void AddChild(IK3Component comp)
        {
            comp.recttransform.SetParent(recttransform, false);
        }

        public void AddChild(RectTransform rtf)
        {
            rtf.SetParent(recttransform, false);
        }

        public bool IsTouchOnMe(Vector2 position, bool checkChild = true)
        {
            bool value = raycastTarget && K3EngineUtils.IsTouchInRect(this, position);

            if (checkChild)
            {
                Graphic[] comps = recttransform.GetComponentsInChildren<Graphic>();
                foreach (var comp in comps)
                {
                    if (comp.raycastTarget && K3EngineUtils.IsTouchInRect(comp, position))
                    {
                        value = true;
                        break;
                    }
                }

            }
            return value;
        }

        public new K3Panel Clone()
        {
            K3Panel panel = UnityEngine.Object.Instantiate<K3Panel>(this);
            return panel;
        }

        public K3PanelEx CloneForLua()
        {
            K3PanelEx panelex = this.GetComponent<K3PanelEx>();
            if (panelex == null)
            {
                panelex = this.addComponent<K3PanelEx>();
            }
            panelex.LoadImmediate(this.gameObject);
            return panelex;
        }


        #region pointer up & down
        protected K3PointerUpHandler OnPointerUpHandler;
        protected K3PointerDownHandler OnPointerDownHandler;
        public void AddPointerUpHandler(K3PointerUpHandler handler)
        {
            OnPointerUpHandler += handler;
        }

        public void AddPointerDownHandler(K3PointerDownHandler handler)
        {
            OnPointerDownHandler += handler;
        }

        public void RemovePointerUpHandler(K3PointerUpHandler handler)
        {
            OnPointerUpHandler -= handler;
        }

        public void RemovePointerDownHandler(K3PointerDownHandler handler)
        {
            OnPointerDownHandler -= handler;
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (OnPointerDownHandler != null)
            {
                OnPointerDownHandler(K3ClickArgs.GetSharedClickArgs(eventData, this, customData));
                K3ClickArgs.Clear();
            }
            if (resposePress)
            {
                OnPressed(true);
            }
            if (m_raycastTargetAll)
            {
                foreach (var pair in m_childrenGraphics)
                {

                    if (pair.Value.IsDestroyed() || !pair.Value.enabled || !pair.Value.gameObject.activeSelf || !pair.Value.gameObject.IsParentVisible(this.gameObject))
                    {
                        continue;
                    }

                    if (pair.Value.IsTouchInRect(eventData.position))
                    {
                        IPointerDownHandler ipd = pair.Value.gameObject.GetComponent<IPointerDownHandler>();
                        if (ipd != null)
                        {
                            ipd.OnPointerDown(eventData);
                            m_pointerDownGraphics[pair.Value.GetInstanceID()] = pair.Value;
                        }
                    }
                }
            }
            m_pressDownTime = Time.time;
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (OnPointerUpHandler != null)
            {
                OnPointerUpHandler(eventData);
            }
            if (resposePress)
            {
                OnPressed(false);
            }
            if (m_raycastTargetAll)
            {
                if (recttransform.IsTouchInRect(eventData.position))
                {
                    foreach (var pair in m_childrenGraphics)
                    {
                        if (pair.Value.IsDestroyed() || !pair.Value.enabled || !pair.Value.gameObject.activeSelf || !pair.Value.gameObject.IsParentVisible(this.gameObject))
                        {
                            continue;
                        }
                        if (m_pointerDownGraphics.ContainsKey(pair.Value.GetInstanceID()))
                        {
                            IPointerUpHandler ipu = pair.Value.gameObject.GetComponent<IPointerUpHandler>();
                            if (ipu != null)
                            {
                                ipu.OnPointerUp(eventData);
                            }
                        }
                    }
                }
            }
            m_pressDownTime = 0;
        }

        protected virtual void OnPressed(bool isPressed)
        {
            if (isPressed)
            {
                rectTransform.DOScale(m_oldScale * 0.94f, 0.01f);
            }
            else
            {
                rectTransform.DOScale(m_oldScale, 0.01f);
            }
        }
        #endregion

        #region drag
        protected bool m_dragable = false;

        public bool dragable
        {
            set
            {
                m_dragable = value;
            }
            get
            {
                return m_dragable;
            }
        }

        private DragHandlerBehavour m_dragBehavour;
        public DragHandlerBehavour dragBehavour
        {
            set
            {
                m_dragBehavour = value;
            }
            get
            {
                return m_dragBehavour;
            }
        }

        public void SetDragable(bool value)
        {
            m_dragable = value;
            if (m_dragable)
            {
                if (m_dragBehavour == null)
                {
                    m_dragBehavour = gameObject.AddComponent<DragHandlerBehavour>();
                    m_dragBehavour.target = this;
                    m_dragBehavour.SetDragCallback(OnBeginDragHandler, OnDragHandler, OnEndDragHandler);
                }
            }
            else
            {
                GameObject.DestroyImmediate(m_dragBehavour);
                m_dragBehavour = null;
            }
        }

        public virtual void OnDragHandler(PointerEventData eventData)
        {
            if (m_raycastTargetAll)
            {
                foreach (var pair in m_childrenGraphics)
                {
                    if (pair.Value.IsDestroyed() || !pair.Value.enabled || !pair.Value.gameObject.activeSelf || !pair.Value.gameObject.IsParentVisible(this.gameObject))
                    {
                        continue;
                    }
                    if (!m_dragedGraphics.ContainsKey(pair.Key) && pair.Value.IsTouchInRect(eventData.position))
                    {
                        IBeginDragHandler ibd = pair.Value.GetComponent<IBeginDragHandler>();
                        if (ibd != null)
                        {
                            ibd.OnBeginDrag(eventData);
                            m_dragedGraphics.Add(pair.Key, pair.Value);
                        }
                    }
                }
                foreach (var pair in m_dragedGraphics)
                {
                    IDragHandler id = pair.Value.GetComponent<IDragHandler>();

                    if (id != null)
                    {
                        id.OnDrag(eventData);
                    }
                }
            }
        }

        public virtual void OnBeginDragHandler(PointerEventData eventData)
        {
            if (m_raycastTargetAll)
            {
                foreach (var pair in m_pointerDownGraphics)
                {
                    IPointerUpHandler ipu = pair.Value.GetComponent<IPointerUpHandler>();
                    if (ipu != null)
                    {
                        ipu.OnPointerUp(eventData);
                    }
                }
                foreach (var pair in m_childrenGraphics)
                {
                    if (pair.Value.IsDestroyed() || !pair.Value.enabled || !pair.Value.gameObject.activeSelf || !pair.Value.gameObject.IsParentVisible(this.gameObject))
                    {
                        continue;
                    }

                    if (pair.Value.IsTouchInRect(eventData.position))
                    {
                        IBeginDragHandler ibd = pair.Value.GetComponent<IBeginDragHandler>();
                        if (ibd != null)
                        {
                            ibd.OnBeginDrag(eventData);
                            m_dragedGraphics.Add(pair.Key, pair.Value);
                        }
                    }
                }
            }
        }

        public virtual void OnEndDragHandler(PointerEventData eventData)
        {
            if (m_raycastTargetAll)
            {
                foreach (var pair in m_dragedGraphics)
                {
                    IEndDragHandler ied = pair.Value.GetComponent<IEndDragHandler>();
                    if (ied != null)
                    {
                        ied.OnEndDrag(eventData);
                    }
                }
                m_dragedGraphics.Clear();
            }
        }

        public void AddDragHandler(K3DragHandler handler)
        {
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onDragingHandler += handler;
            }
        }

        public void AddBeginDragHandler(K3DragHandler handler)
        {
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onBeginDragHandler += handler;
            }
        }

        public void AddEndDragHandler(K3DragHandler handler)
        {
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onEndDragHandler += handler;
            }
        }

        public void RemoveDragHandler(K3DragHandler handler)
        { 
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onDragingHandler -= handler;
            }
        }

        public void RemoveBeginDragHandler(K3DragHandler handler)
        {
	
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onBeginDragHandler -= handler;
            }
        }

        public void RemoveEndDragHandler(K3DragHandler handler)
        {
	
            if (m_dragBehavour != null && m_dragable)
            {
                m_dragBehavour.onEndDragHandler -= handler;
            }
        }
        #endregion

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (m_polygonCollider == null)
            {
                return base.IsRaycastLocationValid(screenPoint, eventCamera);
            }

            return m_polygonCollider.OverlapPoint(eventCamera.ScreenToWorldPoint(screenPoint));
        }

        public K3InfinityListItem CloneInfinityItem()
        {
            K3InfinityListItem panelex = this.GetComponent<K3InfinityListItem>();
            if (panelex == null)
            {
                panelex = this.addComponent<K3InfinityListItem>();
            }
            else
            {
                return panelex.CloneForLua() as K3InfinityListItem;
            }
            panelex.LoadImmediate(this.gameObject);
            return panelex;
        }

        protected void Update()
        {
            if (m_pressDownTime > 0 && Time.time - m_pressDownTime >= m_longPressTime)
            {
                m_pressDownTime = 0;
                if (m_longPressHandler != null)
                {
                    m_longPressHandler.Invoke(K3EventArgs.GetSharedArgs(this, this.customData));
                    K3EventArgs.Clear();
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if(m_scaleTweener != null)
            {
                m_scaleTweener.Kill();
                m_scaleTweener = null;
            }

            if(m_moveTweener != null)
            {
                m_moveTweener.Kill();
                m_moveTweener = null;
            }

            if(m_fadeTweener != null)
            {
                m_fadeTweener.Kill();
            }
        }


    }

    public class DragHandlerBehavour : UIBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public K3DragHandler onBeginDragHandler;
        public K3DragHandler onEndDragHandler;
        public K3DragHandler onDragingHandler;
        public IK3Component target;

        private Action<PointerEventData> m_beginDragCb;

        [HotFixIgnore]
        public Action<PointerEventData> beginDragCb
        {
            set { m_beginDragCb = value; }
            get { return m_beginDragCb; }
        }
        private Action<PointerEventData> m_dragCb;

        [HotFixIgnore]
        public Action<PointerEventData> dragCb
        {
            set { m_dragCb = value; }
            get { return m_dragCb; }
        }
        private Action<PointerEventData> m_endDragCb;

        [HotFixIgnore]
        public Action<PointerEventData> endDragCb
        {
            set { m_endDragCb = value; }
            get { return m_endDragCb; }
        }

        public void SetDragCallback(Action<PointerEventData> begin, Action<PointerEventData> drag, Action<PointerEventData> end)
        {
            m_beginDragCb = begin;
            m_dragCb = drag;
            m_endDragCb = end;
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            if (onBeginDragHandler != null)
            {
                onBeginDragHandler.Invoke(K3DragArgs.GetSharedDragArgs(eventData, target));
                K3DragArgs.Clear();
            }

            if (m_beginDragCb != null)
            {
                m_beginDragCb(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (onDragingHandler != null)
            {
                onDragingHandler.Invoke(K3DragArgs.GetSharedDragArgs(eventData, target));
                K3DragArgs.Clear();
            }
            if (m_dragCb != null)
            {
                m_dragCb(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (onEndDragHandler != null)
            {
                onEndDragHandler.Invoke(K3DragArgs.GetSharedDragArgs(eventData, target));
                K3DragArgs.Clear();
            }
            if (m_endDragCb != null)
            {
                m_endDragCb(eventData);
            }
        }
    }
}
