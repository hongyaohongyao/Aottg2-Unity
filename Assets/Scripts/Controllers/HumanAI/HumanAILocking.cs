
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;
using System;
using Settings;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAILocking : HumanAIAutomatonState
        {
            protected bool _isAttacked;
            public HumanAILocking(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override void StateStart()
            {
                _isAttacked = false;
            }

            public override AutomationState StateAction()
            {
                var Target = _controller.Target;
                if (!_controller.IsTargetValid())
                {
                    return Automation.DefaultState;
                }
                if (_controller.NeedReload())
                {
                    _controller.Reload();
                    return this;
                }
                else if (_human.State == HumanState.Reload)
                {
                    return this;
                }
                else if (_controller.NeedRefill())
                {
                    return Automation.DefaultState;
                }
                if (Vector3.Distance(_human.transform.position, _controller.TargetPosition) > _controller.LockingDistance)
                {
                    return Automation.GetState(HumanAIStates.Finding);
                }
                if (!_human.Grounded)
                {
                    _controller.Jump();
                }
                if (Target is Human h)
                {
                    return LockingHuman(h);
                }
                else if (Target is BaseTitan t)
                {
                    return LockingTitan(t);
                }
                return Automation.DefaultState;
            }

            public void Attack()
            {
                if (!_isAttacked)
                {
                    _isAttacked = true;
                    _controller.Attack();
                }
                else
                {
                    _isAttacked = false;
                }
            }

            public void AttackEnemy(bool isLookingTarget = true)
            {
                if (!isLookingTarget)
                {
                    return;
                }
                var aimPos = _controller.TargetPosition;
                var currentDistance = _controller.TargetDirection.magnitude;
                if (_human.Weapon is BladeWeapon)
                {
                    if (currentDistance <= 5.0f)
                    {
                        Attack();
                    }
                }
                else if (_human.Weapon is ThunderspearWeapon thunderspearWeapon)
                {
                    var shootDistance = thunderspearWeapon.Speed * thunderspearWeapon.TravelTime;
                    if (SettingsManager.InGameCurrent.Misc.ThunderspearPVP.Value)
                    {
                        if (currentDistance <= shootDistance)
                        {
                            if (thunderspearWeapon.GetCooldownLeft() <= 0f)
                            {
                                _controller.Attack();
                            }
                            else if (_human.IsHookedAny())
                            {
                                _controller.Reel(0);
                                _controller.ReelOut();
                            }
                        }
                    }
                    else
                    {
                        if (currentDistance <= shootDistance)
                        {
                            if (!thunderspearWeapon.HasActiveProjectile() || thunderspearWeapon.Current.IsEmbed)
                            {
                                _controller.Attack();
                            }
                        }
                    }
                    aimPos = HumanAIController.CorrectShootPosition(_human.transform.position,
                                                                    _controller.TargetPosition,
                                                                    _controller.TargetVelocity,
                                                                    thunderspearWeapon.Speed);
                }
                else if (_human.Weapon is AmmoWeapon)
                {
                    if (currentDistance <= 20.0f)
                    {
                        Attack();
                    }
                }
                _controller.SetAimPoint(aimPos);
            }

            public AutomationState LockingHuman(Human target)
            {
                var humanPosition = _human.transform.position;
                var targetPosition = _controller.TargetPosition;
                var targetDirection = _controller.TargetDirection;
                var hookedTargetL = _controller.IsHookedTarget(_human.HookLeft);
                var hookedTargetR = _controller.IsHookedTarget(_human.HookRight);
                if (hookedTargetL || hookedTargetR)
                {
                    if (!hookedTargetL)
                    {
                        _controller.ReleaseHookLeft();
                    }
                    if (!hookedTargetR)
                    {
                        _controller.ReleaseHookRight();
                    }
                    var reelInCond = _human.Pivot;
                    if (reelInCond)
                    {
                        _controller.ReelIn();
                    }
                    else
                    {
                        _controller.RandomJump(false, -1.0f);
                    }


                    if (_human.Cache.Rigidbody.velocity.magnitude < 0.5f)
                    {
                        _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 30f);
                    }

                    AttackEnemy();
                    return this;
                }
                var start = humanPosition + targetDirection.normalized;
                var end = humanPosition + targetDirection.normalized * 120f;
                if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                {
                    if (_controller.IsDirectlySeeingTarget(result, 1f))
                    {
                        var correctPosition = targetPosition;
                        correctPosition = HumanAIController.CorrectShootPosition(humanPosition, correctPosition, _controller.TargetVelocity, _controller.HookSpeed);
                        if (_human.HookRight.HookReady())
                        {
                            _controller.LaunchHookRight(correctPosition);
                        }
                        _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 60f);
                    }
                    else
                    {
                        _controller.StraightFlight(_controller.FindTempTarget(result, 1f, 25f) ?? targetPosition, 60f);
                    }
                    AttackEnemy();
                }
                else
                {
                    _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 60f);
                }
                return this;
            }
            public float GetNapeAngle(BaseTitan titan)
            {
                var nape = titan.BaseTitanCache.NapeHurtbox.transform;
                var a = nape.position - _human.transform.position;
                a.y = 0f;
                var b = new Vector3(nape.forward.x, 0f, nape.forward.z);
                return Vector3.SignedAngle(a, b, Vector3.up);
            }

            public AutomationState LockingTitan(BaseTitan target)
            {
                var humanPosition = _human.transform.position;
                var targetPosition = _controller.TargetPosition;
                var targetDirection = _controller.TargetDirection;
                var start = humanPosition + targetDirection.normalized;
                var end = humanPosition + targetDirection.normalized * 120f;
                var hookPosition = _controller.GetHookPosition();
                if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                {
                    if (Mathf.Abs(GetNapeAngle(target)) < 60f)
                    {
                        var hookedTargetL = _controller.IsHookedTarget(_human.HookLeft, true);
                        var hookedTargetR = _controller.IsHookedTarget(_human.HookRight, true);
                        if (hookedTargetL || hookedTargetR)
                        {
                            if (!hookedTargetL)
                            {
                                _controller.ReleaseHookLeft();
                            }
                            if (!hookedTargetR)
                            {
                                _controller.ReleaseHookRight();
                            }

                            float predOpp = Mathf.Infinity;

                            var reelInCond = _human.Pivot;
                            if (reelInCond)
                            {
                                var newV = HumanAIController.CalcReelVelocity(
                                    humanPosition,
                                    hookPosition,
                                    _human.Cache.Rigidbody.velocity,
                                    -1f
                                );
                                predOpp = HumanAIController.PredictAttackOpportunity(
                                    humanPosition,
                                    targetPosition,
                                    newV,
                                    _controller.GetHookPosition(),
                                    5f, 0.5f, 5f
                                );
                                if (Mathf.Abs(predOpp - 0.15f) < 1f)
                                    _controller.ReelIn();
                            }
                            else
                            {
                                HumanAIController.PredictAttackOpportunity(
                                    humanPosition,
                                    targetPosition,
                                    _human.Cache.Rigidbody.velocity,
                                    _controller.GetHookPosition(),
                                    5f, 0.5f, 5f
                                );
                            }


                            if (_human.Cache.Rigidbody.velocity.magnitude < 0.5f)
                            {
                                _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 30f);
                            }

                            if (Mathf.Abs(predOpp - 0.15f) < 1f)
                            {
                                AttackEnemy();
                            }
                            if (_isAttacked)
                            {
                                _controller.ReelOut();
                            }
                            return this;
                        }
                        var correctPosition = target.BaseTitanCache.MouthHitbox.transform.position;
                        correctPosition = HumanAIController.CorrectShootPosition(humanPosition, correctPosition, _controller.TargetVelocity, _controller.HookSpeed);
                        if (_human.HookRight.HookReady())
                        {
                            _controller.LaunchHookRight(correctPosition);
                        }
                        _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 90f);
                    }
                    else
                    {
                        _controller.StraightFlight(_controller.FindTempTarget(result, 1f, 50f) ?? targetPosition, 90f);
                    }
                    AttackEnemy();
                }
                else
                {
                    _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 90f);
                }
                return this;
            }
        }
    }
}