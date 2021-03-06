﻿#region Header
/*	============================================
 *	작성자 : Strix
 *	작성일 : 2018-05-12 오후 12:50:39
 *	기능 : 
   ============================================ */
#endregion Header

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(CPlatformerCalculator))]
public class CPlatformerController : CObjectBase
{
    /* const & readonly declaration             */

    float const_fAccelerationTimeAirborne = .2f;
    float const_fAccelerationTimeGrounded = .1f;
    
    /* enum & struct declaration                */

    public interface ICharacterController_Listener
    {
        void ICharacterController_Listener_OnChangeState(EPlatformerState eStatePrev, EPlatformerState eStateChanged);
        void ICharacterController_Listener_OnChangeFaceDir(int iOneIsLeft_Direction);
        void ICharacterController_Listener_OnSlopeSliding(bool bIsSlopeSliding, float fSlopeAngle);
    }

    public enum EPlatformerState
    {
        Standing,
        Walking,
        Running,

        Crouching,
        CrouchWalking,


        Slope_Sliding,
        Wall_Sliding,

        Jumping,
        MultipleJumping,

        Falling,

        LedgeGrab,
    }

    /* public - Field declaration            */

    public bool _bIsDebuging = false;

    [Header("플랫포머 옵션 - 점프")]
    [Rename_Inspector("점프시 정점 최대 높이")]
    public float p_fMaxJumpHeight = 4;
    [Rename_Inspector("점프시 정점 최소 높이")]
    public float p_fMinJumpHeight = 1;
    [Rename_Inspector("점프시 정점 높이까지 도달하는 시간")]
    public float p_fTimeToJumpApex = .4f;
    [Rename_Inspector("점프 중 몇번이나 더 점프할 수 있는지 - 3이면 3단점프")]
    public int p_iMultipleJumpCount = 3;
    [Rename_Inspector("추가 중력 속도")]
    public float p_fGravityAdjust;


    [Header("플랫포머 옵션 - 움직이기")]
    [Rename_Inspector("정지 시 천천히 멈추기 유무")]
    public bool p_bIsSmoothMoveStop = false;
    [Rename_Inspector("정지 시 멈추는데 걸리는 시간")]
    public float p_fTimeToStop = 0.1f;


    [Rename_Inspector("걷기 속도")]
    public float p_fWalkingSpeed = 2f;
    [Rename_Inspector("달리기 속도")]
    public float p_fRunningSpeed = 4f;
    [Rename_Inspector("걷기로 도달하는 최대 Move Delta")]
    public float p_fMaxMoveDelta_OnWalking = 0.5f;
    [Rename_Inspector("달리기까지 도달하는 시간")]
    public float p_fTimeToRunningApex = 1f;
    [Rename_Inspector("떨어지는 상태까지 도달하는 시간")]
    public float p_fTimeToFalling = 1f;

    [Header("플랫포머 옵션 - 절벽 슬라이딩 중")]
    [Rename_Inspector("방향없이 점프할 때 추가  속도")]
    public Vector2 wallJumpOff;
    [Rename_Inspector("절벽 슬라이딩 시 최대 속도")]
    public float wallSlideSpeedMax = 3;

    public float wallStickTime = .25f;

    [Header("플랫포머 옵션 - 언덕슬라이딩 중 점프 시")]
    [Rename_Inspector("슬라이딩 방향 점프할 때 추가 속도")]
    public Vector2 slopeJumpClimb;
    [Rename_Inspector("방향없이 점프할 때 추가 속도")]
    public Vector2 slopeJumpOff;
    [Rename_Inspector("슬라이딩 반대방향 점프할 때 추가 속도")]
    public Vector2 slopeLeap;

    public CPlatformerCalculator p_pCalculator { get; private set; }

    public EPlatformerState p_ePlatformerState_Current { get; private set; }
    public EPlatformerState p_ePlatformerState_Prev { get; private set; }

    public Vector2 p_vecInputDirection { get; private set; }

    /* protected & private - Field declaration         */

    List<ICharacterController_Listener> _listListener = new List<ICharacterController_Listener>();
    Rigidbody _pRigidbody;
    protected float _fWallSlidingSec;

    int _iInput_LastDirectionX_OneIsLeft;

    float timeToWallUnstick;
    float _fGravity;

    float maxJumpVelocity;
    float minJumpVelocity;
    float velocityXSmoothing;
    
    Vector3 _vecLerpPos;
    private Vector3 _vecVelocity;
    Coroutine _pCoroutine_Falling;
    int _iWallDirX_OneIsRight;

    bool _bIsPossible_ThoughDown;
    bool _bIsMoving;
    bool _bIsCrouching;
    bool _bIsWallSliding;
    bool _bIsJumping;
    bool _bIsFalling;
    bool _bIsLedgeGrab;

    int _iJumpCount;

    float _fGravityDelta_0_1;
    float _fMoveDelta_0_1;
    float _fLerpDelta_0_1;

    // ========================================================================== //

    /* public - [Do] Function
     * 외부 객체가 호출(For External class call)*/
     
    public void DoAdd_Listener(ICharacterController_Listener pListener)
    {
        if (_listListener.Contains(pListener))
            return;

        _listListener.Add(pListener);
    }

    public void DoRemove_Listener(ICharacterController_Listener pListener)
    {
        _listListener.Remove(pListener);
    }

    public void DoInputVelocity(Vector2 vecInput, bool bIsRunning)
    {
        p_vecInputDirection = vecInput;
        _bIsCrouching = vecInput.y < 0f;
        _bIsMoving = vecInput.x != 0f;

        if (_bIsMoving)
        {
            if (vecInput.x < 0f)
                _iInput_LastDirectionX_OneIsLeft = -1;
            else if(vecInput.x > 0f)
                _iInput_LastDirectionX_OneIsLeft = 1;

            if (bIsRunning)
                _fMoveDelta_0_1 = 1f;
        }
        else
        {
            if (p_bIsSmoothMoveStop == false)
                _fMoveDelta_0_1 = 0f;
        }

    }
    
    public void DoJumpInputDown()
    {
        if (_bIsPossible_ThoughDown && _bIsCrouching)
        {
            p_pCalculator.DoIgnoreCollider(0.5f);
            _bIsFalling = true;
            //_vecVelocity.y -= maxJumpVelocity;
            return;
        }

        if (++_iJumpCount > p_iMultipleJumpCount)
            return;
        
        if (_bIsWallSliding)
        {
            CalculateJump_OnWallSliding(out _bIsJumping);
            return;
        }
        _bIsJumping = true;

        if (p_pCalculator.p_pCollisionInfo.slidingDownMaxSlope)
        {
            Vector3 vecSlopeNormal = p_pCalculator.p_pCollisionInfo.slopeNormal.normalized;
            int iSlopeDirX = vecSlopeNormal.x < 0 ? -1 : 1;

            _vecVelocity.y = maxJumpVelocity * p_pCalculator.p_pCollisionInfo.slopeNormal.y;
            _vecVelocity.x = maxJumpVelocity * p_pCalculator.p_pCollisionInfo.slopeNormal.x;

            if (iSlopeDirX == p_vecInputDirection.x )
            {
                _vecVelocity.x += -iSlopeDirX * slopeJumpClimb.x;
                _vecVelocity.y += slopeJumpClimb.y;
            }
            else if (p_vecInputDirection.x == 0)
            {
                _vecVelocity.x += -iSlopeDirX * slopeJumpOff.x;
                _vecVelocity.y += slopeJumpOff.y;
            }
            else
            {
                _vecVelocity.x += -iSlopeDirX * slopeLeap.x;
                _vecVelocity.y += slopeLeap.y;
            }

            //{ // not jumping against max slope
            //    _vecVelocity.y = maxJumpVelocity * _pCalculator.collisions.slopeNormal.y;
            //    _vecVelocity.x = maxJumpVelocity * _pCalculator.collisions.slopeNormal.x;
            //}
        }
        else
        {
            _vecVelocity.y = maxJumpVelocity;
        }
    }

    public void DoJumpInputUp()
    {
        if (_vecVelocity.y > minJumpVelocity * _iJumpCount)
        {
            _vecVelocity.y = minJumpVelocity;
        }
    }

    public void Event_StartClimbing(Transform pPlatformTarget, Collision pCollisionInfo)
    {
        Climbing(pPlatformTarget, p_pCalculator.p_pCollider2D, pCollisionInfo.collider.bounds, pCollisionInfo.contacts[0].point);
    }

    public void Event_StartClimbing(Transform pPlatformTarget, Collision2D pCollisionInfo)
    {
        Climbing(pPlatformTarget, p_pCalculator.p_pCollider2D, pCollisionInfo.collider.bounds, pCollisionInfo.contacts[0].point);
    }

    private void Climbing(Transform pPlatformTarget, Behaviour pCollider, Bounds pBounds, Vector3 vecHitPoint)
    {
        if (_bIsLedgeGrab) return;

        Vector3 vecBoundsMin_Other = pBounds.min;
        Vector3 vecBoundsMax_Other = pBounds.max;
        Vector2 vecPlatformDirection = vecHitPoint - transform.position;

        // 인풋 방향과 같을 때
        // 방향이 오른쪽인 경우
        // 상대방의 min.x max.y 위치로 러프시키며 
        // 등반을 시작한다.
        if ((vecPlatformDirection.x > 0f && _iWallDirX_OneIsRight == 1) ||
            (vecPlatformDirection.x < 0f && _iWallDirX_OneIsRight != 1))
        {
            if (_iWallDirX_OneIsRight != 1)
                _vecLerpPos = new Vector3(vecBoundsMax_Other.x + 0.4f, vecBoundsMax_Other.y);
            else
                _vecLerpPos = new Vector3(vecBoundsMin_Other.x - 0.4f, vecBoundsMax_Other.y);

            _fLerpDelta_0_1 = 0f;
            _bIsLedgeGrab = true;
            //pCollider.enabled = false;
        }
    }

    public void Event_FinishLedgeGrab(Vector3 vecOffsetPos)
    {
        if (p_pCalculator.p_pCollisionInfo.iFaceDir_OneIsLeft != 1)
            vecOffsetPos.x *= -1f;

        Vector3 vecPos = transform.position;
        vecPos += vecOffsetPos;
        transform.position = vecPos;

        _bIsLedgeGrab = false;
        SetState();
        _vecVelocity = Vector3.zero;
        //p_pCalculator.p_pCollider.enabled = true;
    }
    
    // ========================================================================== //

    /* protected - Override & Unity API         */

    protected override void OnAwake()
    {
        base.OnAwake();

        p_pCalculator = GetComponent<CPlatformerCalculator>();
        p_pCalculator.DoSet_OnChangeFaceDir(SetOnChangeFaceDir);
        p_pCalculator.DoSet_OnChangeBelow(OnChangeBelow);
        p_pCalculator.DoSet_OnChangeSlopeSliding(OnChangeSlopeSliding);
        p_pCalculator.DoSet_CalcaulateVerticalCollider(OnCalculateVerticalCollider);

        _fGravity = -(2 * p_fMaxJumpHeight) / Mathf.Pow(p_fTimeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(_fGravity) * p_fTimeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(_fGravity) * p_fMinJumpHeight);

        _pRigidbody = GetComponent<Rigidbody>();
    }

    public override void OnUpdate(ref bool bCheckUpdateCount)
    {
        base.OnUpdate(ref bCheckUpdateCount);
        bCheckUpdateCount = true;

        if (_bIsLedgeGrab)
        {
            if (_fLerpDelta_0_1 <= 1f)
            {
                //transform.position = Vector3.Lerp(transform.position, _vecLerpPos, _fLerpDelta_0_1);
                _fLerpDelta_0_1 += 0.2f * Time.deltaTime;
            }

            OnChangeState(EPlatformerState.LedgeGrab);
            return;
        }

        CalculateVelocity();
        HandleWallSliding();

        p_pCalculator.DoMove(_vecVelocity * Time.deltaTime, p_vecInputDirection);
        if (p_pCalculator.p_pCollisionInfo.above || p_pCalculator.p_pCollisionInfo.below)
        {
            if (p_pCalculator.p_pCollisionInfo.slidingDownMaxSlope)
            {
                _vecVelocity.y += p_pCalculator.p_pCollisionInfo.slopeNormal.y * -CalculateGravity() * Time.deltaTime;
            }
            else
            {
                _vecVelocity.y = 0;
            }
        }

        if (p_pCalculator.p_pCollisionInfo.below == false && _fGravityDelta_0_1 < 1f)
        {
            _fGravityDelta_0_1 += Time.deltaTime;
            if (_fGravityDelta_0_1 > 1f)
                _fGravityDelta_0_1 = 1f;
        }

        if (_bIsMoving && _fMoveDelta_0_1 < p_fMaxMoveDelta_OnWalking)
        {
            _fMoveDelta_0_1 += (1f / p_fTimeToRunningApex) * Time.deltaTime;
            if (_fMoveDelta_0_1 > p_fMaxMoveDelta_OnWalking)
                _fMoveDelta_0_1 = p_fMaxMoveDelta_OnWalking;
        }

        if (_bIsMoving == false && p_bIsSmoothMoveStop)
        {
            if (_fMoveDelta_0_1 > 0f)
                _fMoveDelta_0_1 -= (1f / p_fTimeToStop) * Time.deltaTime;
        }

        if (_vecVelocity.x == 0f)
            velocityXSmoothing = 0f;

        SetState();
    }

    private void SetState()
    {
        if (_bIsFalling)
        {
            OnChangeState(EPlatformerState.Falling);
            return;
        }

        if (p_pCalculator.p_pCollisionInfo.slidingDownMaxSlope)
        {
            OnChangeState(EPlatformerState.Slope_Sliding);
            return;
        }

        if (_bIsWallSliding)
        {
            OnChangeState(EPlatformerState.Wall_Sliding);
            return;
        }

        if (_bIsJumping)
        {
            if (_iJumpCount == 1)
                OnChangeState(EPlatformerState.Jumping);
            else
                OnChangeState(EPlatformerState.MultipleJumping);
            return;
        }


        if (_bIsMoving)
        {
            if (_fMoveDelta_0_1.Equals(1f))
                OnChangeState(EPlatformerState.Running);
            else
            {
                if (_bIsCrouching)
                    OnChangeState(EPlatformerState.CrouchWalking);
                else
                    OnChangeState(EPlatformerState.Walking);
            }
        }
        else
        {
            if (_bIsCrouching)
                OnChangeState(EPlatformerState.Crouching);
            else
                OnChangeState(EPlatformerState.Standing);
            return;
        }
    }

#if UNITY_EDITOR
    Vector3 vecDebugOffset = new Vector2(1f, 1f);

    private void OnDrawGizmos()
    {
        if (_bIsDebuging == false) return;

        Vector3 vecPos = transform.position + vecDebugOffset;

        UnityEditor.Handles.Label(vecPos, "velocity : " + _vecVelocity.ToString("F4"));
        vecPos.y -= 0.5f;

        UnityEditor.Handles.Label(vecPos, "_fMoveDelta_0_1 : " + _fMoveDelta_0_1);
        vecPos.y -= 0.5f;

        UnityEditor.Handles.Label(vecPos, "_ePlatformerState_Flags_Prev : " + p_ePlatformerState_Prev);
        vecPos.y -= 0.5f;

        UnityEditor.Handles.Label(vecPos, "_ePlatformerState_Flags_Current : " + p_ePlatformerState_Current);
        vecPos.y -= 0.5f;

        UnityEditor.Handles.Label(vecPos, "_iJumpCount : " + _iJumpCount);
        vecPos.y -= 0.5f;

        UnityEditor.Handles.Label(vecPos, "_fWallSlidingSec : " + _fWallSlidingSec);
        vecPos.y -= 0.5f;

        if (p_pCalculator != null)
            UnityEditor.Handles.Label(vecPos, "_pCalculator.collisions.slopeNormal : " + p_pCalculator.p_pCollisionInfo.slopeNormal);
    }
#endif

    /* protected - [abstract & virtual]         */

    virtual protected void OnChangeState(EPlatformerState eState)
    {
        if (p_ePlatformerState_Current != eState)
            p_ePlatformerState_Prev = p_ePlatformerState_Current;

        p_ePlatformerState_Current = eState;
        for (int i = 0; i < _listListener.Count; i++)
            _listListener[i].ICharacterController_Listener_OnChangeState(p_ePlatformerState_Prev, p_ePlatformerState_Current);
    }

    virtual protected void HandleWallSliding()
    {
        _iWallDirX_OneIsRight = (p_pCalculator.p_pCollisionInfo.left) ? -1 : 1;

        bool bIsWallSliding = CheckIs_WallSliding();
        if (bIsWallSliding != _bIsWallSliding)
            _fWallSlidingSec = 0f;
        else
        {
            if (_bIsWallSliding)
                _fWallSlidingSec += Time.deltaTime;
        }

        _bIsWallSliding = bIsWallSliding;
        if (_bIsWallSliding)
        {
            if (_vecVelocity.y < -wallSlideSpeedMax)
            {
                _vecVelocity.y = -wallSlideSpeedMax;
            }

            if (timeToWallUnstick > 0)
            {
                velocityXSmoothing = 0;
                _vecVelocity.x = 0;
                _fMoveDelta_0_1 = 0f;
                _bIsMoving = false;

                if (p_vecInputDirection.x != _iWallDirX_OneIsRight && p_vecInputDirection.x != 0)
                {
                    timeToWallUnstick -= Time.deltaTime;
                }
                else
                {
                    timeToWallUnstick = wallStickTime;
                }
            }
            else
            {
                timeToWallUnstick = wallStickTime;
            }

        }

    }

    virtual protected void CalculateVelocity()
    {
        if (p_ePlatformerState_Prev == EPlatformerState.Slope_Sliding && p_ePlatformerState_Current == EPlatformerState.Jumping)
        {

        }
        else
        {
            if (p_bIsSmoothMoveStop)
            {
                float fTargetVelocityX = 0f;
                if (p_vecInputDirection.x != 0f)
                    fTargetVelocityX = p_vecInputDirection.x * Mathf.Lerp(p_fWalkingSpeed, p_fRunningSpeed, _fMoveDelta_0_1);
                else if(_fMoveDelta_0_1 > 0f)
                    fTargetVelocityX = _iInput_LastDirectionX_OneIsLeft * Mathf.Lerp(p_fWalkingSpeed, p_fRunningSpeed, _fMoveDelta_0_1);

                _vecVelocity.x = Mathf.SmoothDamp(_vecVelocity.x, fTargetVelocityX, ref velocityXSmoothing, (p_pCalculator.p_pCollisionInfo.below) ? const_fAccelerationTimeGrounded : const_fAccelerationTimeAirborne);
            }
            else
            {
                float fTargetVelocityX = p_vecInputDirection.x * Mathf.Lerp(p_fWalkingSpeed, p_fRunningSpeed, _fMoveDelta_0_1);
                _vecVelocity.x = Mathf.SmoothDamp(_vecVelocity.x, fTargetVelocityX, ref velocityXSmoothing, (p_pCalculator.p_pCollisionInfo.below) ? const_fAccelerationTimeGrounded : const_fAccelerationTimeAirborne);
            }
        }

        if (p_pCalculator.p_pCollisionInfo.below == false)
            _vecVelocity.y += CalculateGravity() * Time.deltaTime;
    }

    virtual protected void CalculateJump_OnWallSliding(out bool bIsJumping)
    {
        bIsJumping = true;
        if (p_vecInputDirection.x == 0)
        {
            _vecVelocity.x = -_iWallDirX_OneIsRight * wallJumpOff.x;
            _vecVelocity.y = wallJumpOff.y;
        }
    }

    virtual protected float CalculateGravity()
    {
        return (_fGravity * _fGravityDelta_0_1) + (p_fGravityAdjust * -1);
    }

    virtual protected bool CheckIs_WallSliding()
    {
        bool bWall_IsPossible_Sliding = false;
        foreach (var pHit in p_pCalculator.p_pCollisionInfo._listHitTransform)
        {
            //CCompoPlatform pPlatform = pHit.transform.GetComponent<CCompoPlatform>();
            //if (pPlatform != null && pPlatform.ePlatformType == CCompoPlatform.EPlatformType.Sliding)
            //{
            //    bWall_IsPossible_Sliding = true;
            //    break;
            //}
        }

        if (bWall_IsPossible_Sliding == false)
            return false;

        return (p_pCalculator.p_pCollisionInfo.left && p_vecInputDirection.normalized.x < 0 ||
                p_pCalculator.p_pCollisionInfo.right && p_vecInputDirection.normalized.x > 0) &&   // 벽 방향으로 인풋입력이 들어오고, 
                p_pCalculator._iHorizontalRayCount * 0.8 < p_pCalculator._iHorizontalHitCount &&
                !p_pCalculator.p_pCollisionInfo.below && _vecVelocity.y < 0;
    }

    // ========================================================================== //

    #region Private

    private void OnCalculateVerticalCollider(Transform pTransformHit, out CPlatformerCalculator.ECollisionIgnoreType eCollisionIgnoreType)
    {
        eCollisionIgnoreType = CPlatformerCalculator.ECollisionIgnoreType.None;
        //CCompoPlatform pPlatform = pTransformHit.GetComponent<CCompoPlatform>();
        //if (pPlatform != null)
        //{
        //    if (_vecVelocity.y > 0f && _bIsJumping &&
        //        pPlatform.ePlatformType == CCompoPlatform.EPlatformType.Though || pPlatform.ePlatformType == CCompoPlatform.EPlatformType.Though_Direction_Up)
        //    {
        //        eCollisionIgnoreType = CPlatformerCalculator.ECollisionIgnoreType.Though_Up;
        //    }
        //    else if (pPlatform.ePlatformType == CCompoPlatform.EPlatformType.Though || pPlatform.ePlatformType == CCompoPlatform.EPlatformType.Though_Direction_Down)
        //    {
        //        _bIsPossible_ThoughDown = true;
        //    }
        //}
        //else
            _bIsPossible_ThoughDown = false;
    }

    private void SetOnChangeFaceDir(int iFaceDir)
    {
        for (int i = 0; i < _listListener.Count; i++)
            _listListener[i].ICharacterController_Listener_OnChangeFaceDir(iFaceDir);
    }

    private void OnChangeBelow(bool bBelow)
    {
        if (bBelow)
        {
            _iJumpCount = 0;
            _bIsJumping = false;
            _bIsFalling = false;

            if (_pCoroutine_Falling != null)
                StopCoroutine(_pCoroutine_Falling);
        }

        if (CheckIsFalling())
        {
            if (_pCoroutine_Falling != null)
                StopCoroutine(_pCoroutine_Falling);
            _pCoroutine_Falling = StartCoroutine(CoDelayFalling());
        }
    }

    private void OnChangeSlopeSliding(bool bSlopeSliding)
    {
        for (int i = 0; i < _listListener.Count; i++)
            _listListener[i].ICharacterController_Listener_OnSlopeSliding(bSlopeSliding, p_pCalculator.p_pCollisionInfo.slopeAngle);
    }

    private IEnumerator CoDelayFalling()
    {
        yield return new WaitForSeconds(0.5f);

        if (CheckIsFalling())
            SetrFalling();
    }

    private void SetrFalling()
    {
        _bIsFalling = true;
        _iJumpCount++;
    }

    private bool CheckIsFalling()
    {
        return p_pCalculator.p_pCollisionInfo.below == false && _iJumpCount == 0 && _bIsWallSliding == false && p_pCalculator.p_pCollisionInfo.climbingSlope == false;
    }

    #endregion Private
}