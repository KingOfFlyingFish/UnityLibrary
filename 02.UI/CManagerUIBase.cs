﻿#region Header
/* ============================================ 
 *			    Strix Unity Library
 *		https://github.com/strix13/UnityLibrary
 *	============================================ 	
 *	관련 링크 :
 *	
 *	설계자 : 
 *	작성자 : Strix
 *	
 *	기능 : NGUI와 UGUI를 병합하려다 보니
 *	관리하는 객체인 Panel의 변수는 매니져가 관리하고, 함수는 인터페이스로 빼야 했다.
 *	
   ============================================ */
#endregion Header

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public interface IManagerUI
{
	void IManagerUI_ShowHide_Panel( int iPanelHashCode, bool bShow, System.Action OnFinishAnimation = null );
}

abstract public partial class CManagerUIBase<CLASS_Instance, ENUM_Panel_Name, CLASS_Panel, Class_Button> : CSingletonMonoBase<CLASS_Instance>, IManagerUI
	where CLASS_Instance : CManagerUIBase<CLASS_Instance, ENUM_Panel_Name, CLASS_Panel, Class_Button>
    where ENUM_Panel_Name : System.IFormattable, System.IConvertible, System.IComparable
    where CLASS_Panel : CObjectBase, IUIPanel
{
    protected CDictionary_ForEnumKey<ENUM_Panel_Name, CUIPanelData> _mapPanelData = new CDictionary_ForEnumKey<ENUM_Panel_Name, CUIPanelData>();

    public Camera p_pUICamera { get; private set; }
    public int p_iSortOrderTop { get; private set; }

    // ========================== [ Division ] ========================== //

    /// <summary>
    /// 지정된 기본 UI외의 Panel 모두 끕니다.
    /// </summary>
    public void DoShowDefaultPanel()
    {
        List<CUIPanelData> listPanelDataAll = _mapPanelData.Values.ToList();
        for (int i = 0; i < listPanelDataAll.Count; i++)
            listPanelDataAll[i].SetActive(listPanelDataAll[i].p_pPanel.p_bIsAlwaysShow);

        OnDefaultPanelShow();
    }

    public void DoHideAllPanel(bool bAlwaysShowHide = false)
    {
        List<CUIPanelData> listPanelDataAll = _mapPanelData.Values.ToList();
        if (bAlwaysShowHide)
        {
            for (int i = 0; i < listPanelDataAll.Count; i++)
                listPanelDataAll[i].SetActive(false);
        }
        else
        {
            for (int i = 0; i < listPanelDataAll.Count; i++)
                listPanelDataAll[i].SetActive(listPanelDataAll[i].p_pPanel.p_bIsAlwaysShow);
        }
    }

    /// <summary>
    /// 지정된 Panel을 열거나 끕니다.
    /// </summary>
    /// <param name="ePanel">Panel 이름의 Enum</param>
    /// <param name="bShow"></param>
    public CLASS_Panel DoShowHide_Panel(ENUM_Panel_Name ePanel, bool bShow, System.Action OnFinishAnimation = null)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];
        if (pPanel == null)
        {
            Debug.LogWarning(ePanel + "이 없습니다.. 하이어라키를 확인해주세요");
            return null;
        }

        if (bShow)
        {
            int iSortOrder = 0;
            if (pPanel.p_pPanel.p_bIsFixedSortOrder == false)
                iSortOrder = CaculateSortOrder_Top();

            pPanel.DoShow(iSortOrder);
        }
        else
            pPanel.DoHide();

		if(OnFinishAnimation != null)
			pPanel.DoSetFinishAnimationEvent( OnFinishAnimation );

        return pPanel.p_pPanel;
    }

    public CLASS_Panel DoShowHide_Panel_Force(ENUM_Panel_Name ePanel, bool bShow)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];
        if (pPanel == null)
        {
            Debug.LogWarning(ePanel + "이 없습니다.. 하이어라키를 확인해주세요");
            return null;
        }
        pPanel.SetActive(bShow);

        return pPanel.p_pPanel;
    }

    public bool DoCheckIsActive(ENUM_Panel_Name ePanel)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];
        if (pPanel == null)
        {
            Debug.LogWarning(ePanel + "이 없습니다.. 하이어라키를 확인해주세요");
            return false;
        }

        return pPanel.p_bIsShowCurrent;
    }

    public bool DoCheckIsPlayAnimation(ENUM_Panel_Name ePanel)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];
        if (pPanel == null)
        {
            Debug.LogWarning(ePanel + "이 없습니다.. 하이어라키를 확인해주세요");
            return false;
        }

        return pPanel.p_bIsPlayingUIAnimation;
    }

    public void DoChangePanel_FadeInout(ENUM_Panel_Name ePanelHide, ENUM_Panel_Name ePanelShow, float fFadeTime = 1f)
    {
        CUIPanelData pPanelHide = _mapPanelData[ePanelHide];
        CUIPanelData pPanelShow = _mapPanelData[ePanelShow];
        int iSortOrder = 0;
        if (pPanelShow.p_pPanel.p_bIsFixedSortOrder == false)
            iSortOrder = CaculateSortOrder_Top();

        pPanelShow.EventSetOrder(iSortOrder);
        AutoFade.DoStartFade(fFadeTime, Color.black, pPanelHide.DoHide_IgnoreAnimation, pPanelShow.DoShow);
    }

    public void DoShowPanel_FadeIn(ENUM_Panel_Name ePanel, System.Action OnFinishFadeIn, float fFadeTime = 1f)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];

        int iSortOrder = 0;
        if (pPanel.p_pPanel.p_bIsFixedSortOrder == false)
            iSortOrder = CaculateSortOrder_Top();

        pPanel.EventSetOrder(iSortOrder);
        AutoFade.DoStartFade(fFadeTime, Color.black, pPanel.DoShow, OnFinishFadeIn );
    }

    public void DoHidePanel_FadeOut(ENUM_Panel_Name ePanel, System.Action OnFinishFadeOut, float fFadeTime = 1f)
    {
        CUIPanelData pPanel = _mapPanelData[ePanel];
        AutoFade.DoStartFade(fFadeTime, Color.black, pPanel.DoHide_IgnoreAnimation, OnFinishFadeOut );
    }

    /// <summary>
    /// UI Panel을 얻어옵니다.
    /// </summary>
    /// <typeparam name="CUIPanel">얻고자 하는 Panel 타입</typeparam>
    /// <returns></returns>
    public CUIPanel GetUIPanel<CUIPanel>() where CUIPanel : CLASS_Panel
    {
        CUIPanelData pFindPanel = null;
        ENUM_Panel_Name strKey = typeof(CUIPanel).ToString().ConvertEnum<ENUM_Panel_Name>();
        bool bResult = _mapPanelData.TryGetValue(strKey, out pFindPanel);
        if (bResult == false)
            Debug.LogWarning(string.Format("{0}을 찾을 수 없습니다.", strKey));

        return pFindPanel.p_pPanel as CUIPanel;
    }

    /// <summary>
    /// UI Panel을 얻어옵니다.
    /// </summary>
    /// <typeparam name="CUIPanel">얻고자 하는 Panel 타입</typeparam>
    /// <returns></returns>
    public bool GetUIPanel<CUIPanel>(out CUIPanel pUIPanelOut) where CUIPanel : CLASS_Panel
    {
        CUIPanelData pFindPanel = null;
        ENUM_Panel_Name strKey = typeof(CUIPanel).ToString().ConvertEnum<ENUM_Panel_Name>();
        bool bResult = _mapPanelData.TryGetValue(strKey, out pFindPanel);
        if (bResult == false)
        {
            pUIPanelOut = null;
            Debug.Log(string.Format("{0}을 찾을 수 없습니다.", strKey), this);
        }
        else
            pUIPanelOut = pFindPanel.p_pPanel as CUIPanel;

        return bResult;
    }

    /// <summary>
    /// 타겟 카메라의 좌표로 UI 오브젝트를 이동합니다. ( 타겟 카메라의 WorldToView -> UI 카메라의 WorldToView )
    /// </summary>
    /// <param name="pCameraTarget">타겟 카메라</param>
    /// <param name="pTransTarget">이동시킬 UI 오브젝트</param>
    /// <param name="vecTargetPos">이동시킬 좌표</param>
    /// <returns></returns>
    public Vector3 DoSetUIToInGame3D(Camera pCameraTarget, Transform pTransTarget, Vector3 vecTargetPos)
    {
        Vector3 v3UIpos = p_pUICamera.ViewportToWorldPoint(pCameraTarget.WorldToViewportPoint(vecTargetPos));
        pTransTarget.position = new Vector3(v3UIpos.x, v3UIpos.y, 0);
        pTransTarget.localPosition = new Vector3(pTransTarget.localPosition.x, pTransTarget.localPosition.y, 0);

        return pTransTarget.localPosition;
    }

	// ========================== [ Division ] ========================== //

	public void IManagerUI_ShowHide_Panel( int iPanelHashCode, bool bShow, System.Action OnFinishAnimation = null )
	{
		ENUM_Panel_Name ePanelName = _mapPanelData.ConvertHashCodeToEnum( iPanelHashCode );
		DoShowHide_Panel( ePanelName, bShow, OnFinishAnimation );
	}

	// ========================== [ Division ] ========================== //

	protected override void OnAwake()
    {
        base.OnAwake();

        InitUIPanelInstance();
        p_pUICamera = GetComponentInChildren<Camera>();
    }

    protected override void OnEnableObject()
    {
        base.OnEnableObject();

        DoShowDefaultPanel();
    }

    // ========================== [ Division ] ========================== //

    abstract protected void OnDefaultPanelShow();

    // ========================== [ Division ] ========================== //

    private void InitUIPanelInstance()
    {
        CLASS_Panel[] arrPanelInstance = GetComponentsInChildren<CLASS_Panel>(true);
        for (int i = 0; i < arrPanelInstance.Length; i++)
        {
            CLASS_Panel pPanel = arrPanelInstance[i];
            pPanel.EventOnAwake();

			string strComponentName = pPanel.GetType().Name;
            ENUM_Panel_Name ePanelName;
            if (strComponentName.ConvertEnum(out ePanelName))
			{
				pPanel.IUIPanel_Init(this, ePanelName.GetHashCode());
				_mapPanelData.Add( ePanelName, new CUIPanelData( ePanelName, pPanel ) );
			}
		}
    }

    private int CaculateSortOrder_Top()
    {
        p_iSortOrderTop = 0;
        List<CUIPanelData> listUIPanel = _mapPanelData.Values.ToList();

        for (int i = 0; i < listUIPanel.Count; i++)
        {
            CUIPanelData pUIPanelData = listUIPanel[i];
            if (pUIPanelData.p_pPanel.isActiveAndEnabled)
            {
                if (pUIPanelData.p_pPanel.p_bIsFixedSortOrder)
                    ++p_iSortOrderTop;
                else
                    pUIPanelData.EventSetOrder(++p_iSortOrderTop);
            }
        }

        p_iSortOrderTop += 1;
        return p_iSortOrderTop;
    }

	// ========================== [ Division ] ========================== //
}
