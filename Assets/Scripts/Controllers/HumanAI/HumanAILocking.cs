
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
            protected float targetRadius;

            protected float attackDistance;
            public HumanAILocking(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override void StateStart()
            {
                _isAttacked = false;
                if (_human.Weapon is BladeWeapon)
                {
                    attackDistance = 5f;
                }
                else if (_human.Weapon is AmmoWeapon)
                {
                    attackDistance = 20f;
                }
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

            public void Attack(bool isHold = false)
            {
                if (!_isAttacked || isHold)
                {
                    _isAttacked = true;
                    _controller.Attack();
                }
                else
                {
                    _isAttacked = false;
                }
            }

            public void AttackEnemy(bool isLookingTarget = true, bool tryHold = false, bool delayAttack = false)
            {
                if (_human.Weapon is HoldUseable && tryHold && !_isAttacked)
                {
                    Attack(true);
                }

                var aimPos = _controller.TargetPosition;
                var currentDistance = _controller.TargetDirection.magnitude;
                var keepHold = false;
                if (isLookingTarget)
                {
                    if (_human.Weapon is BladeWeapon)
                    {
                        if (delayAttack && _human.Cache.Rigidbody.velocity.magnitude > 0.1f)
                        {
                            var proj = Vector3.Project(_controller.TargetDirection, _human.Cache.Rigidbody.velocity);
                            var t = proj.magnitude / _human.Cache.Rigidbody.velocity.magnitude;
                            if (t < 0.3f)
                            {
                                currentDistance = (_controller.TargetDirection - proj).magnitude;
                            }
                        }
                        if (currentDistance <= attackDistance + targetRadius)
                        {
                            Attack();
                        }
                        else if (tryHold)
                        {
                            keepHold = true;
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
                        if (currentDistance <= attackDistance + targetRadius)
                        {
                            Attack();
                        }
                    }
                }
                else
                {
                    keepHold = true;
                }

                if (_human.Weapon is HoldUseable && keepHold)
                {
                    Attack(true);
                }
                _controller.SetAimPoint(aimPos);
            }

            public AutomationState LockingHuman(Human target)
            {
                targetRadius = 0.5f;
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

            float SignedDistanceToDirection(Vector3 A, Vector3 B, Vector3 forward)
            {
                Vector3 bDir = B.normalized;
                Vector3 projection = Vector3.Dot(A, bDir) * bDir;
                Vector3 rejection = A - projection; // A 到 B 的垂直部分
                float distance = rejection.magnitude;

                // 符号由 forward 和 rejection 方向决定
                float sign = Mathf.Sign(Vector3.Dot(rejection.normalized, forward));
                return sign * distance;
            }

            public AutomationState LockingTitan(BaseTitan target)
            {
                if (_human.Weapon is BladeWeapon)
                {
                    if (!_human.Grounded)
                    {
                        attackDistance = 3f;
                    }
                    else
                    {
                        attackDistance = 5f;
                    }

                }
                // var napeBox = (CapsuleCollider)target.BaseTitanCache.NapeHurtbox;
                // var globalScale = napeBox.transform.lossyScale;
                // var napeRadius = napeBox.radius * Mathf.Max(globalScale.x, globalScale.y);
                targetRadius = 2f;
                var humanPosition = _human.transform.position;
                var targetPosition = _controller.TargetPosition;
                var targetDirection = _controller.TargetDirection;
                // var start = humanPosition + targetDirection.normalized;
                // var end = humanPosition + targetDirection.normalized * 120f;
                var hookPosition = _controller.GetHookPosition();
                var hookedTargetL = _controller.IsHookedTarget(_human.HookLeft, true);
                var hookedTargetR = _controller.IsHookedTarget(_human.HookRight, true);
                var moveBox = (CapsuleCollider)target.BaseTitanCache.Movebox;
                var rawRadius = moveBox.radius * moveBox.transform.lossyScale.x * 2f;
                var safeRadius = rawRadius * 1.2f;
                var radius = rawRadius * 1.4f;
                var velocity = _human.Cache.Rigidbody.velocity;


                var isDirectlySeeingTarget = !Physics.Linecast(humanPosition + targetDirection.normalized, targetPosition, out RaycastHit result, HumanAIController.BarrierMask) || _controller.IsDirectlySeeingTarget(result, 0.1f);
                var flyAround = true;

                Debug.DrawLine(humanPosition, targetPosition, Color.red);

                if (isDirectlySeeingTarget && velocity.magnitude > 0.1f)
                {

                    // var cross = Vector3.Cross(_controller.TargetDirection, velocity);
                    var distance = SignedDistanceToDirection(_controller.TargetDirection, velocity, target.BaseTitanCache.NapeHurtbox.transform.forward);
                    var isDirectlyFlightTo = distance > 0 && distance < attackDistance + targetRadius && !Physics.Linecast(humanPosition + velocity.normalized, humanPosition + Mathf.Abs(distance) * velocity.normalized, out result, HumanAIController.BarrierMask) || _controller.IsDirectlySeeingTarget(result, 0.1f);
                    if (isDirectlyFlightTo)
                    {
                        flyAround = false;
                    }
                    else
                    {
                        var reelVelocity = HumanAIController.CalcReelVelocity(humanPosition, hookPosition, velocity, -1f);
                        distance = SignedDistanceToDirection(_controller.TargetDirection, reelVelocity, target.BaseTitanCache.NapeHurtbox.transform.forward);
                        var isReelFlightTo = distance > 0 && distance < attackDistance + targetRadius && !Physics.Linecast(humanPosition + reelVelocity.normalized, humanPosition + distance * reelVelocity.normalized, out result, HumanAIController.BarrierMask) || _controller.IsDirectlySeeingTarget(result, 0.1f);
                        if (isReelFlightTo)
                        {
                            Debug.Log("Attack Reel");
                            _controller.ReelIn();
                            flyAround = false;
                        }
                    }
                }

                if (flyAround)
                {
                    Debug.Log("scale: " + target.BaseTitanCache.Head.lossyScale.z + " " + safeRadius);
                    var mouthPos = target.BaseTitanCache.MouthHitbox.transform.position;
                    var napePos = target.BaseTitanCache.NapeHurtbox.transform.position;
                    // _controller.FlightAround(target.BaseTitanCache.NapeHurtbox.transform.position, radius, safeRadius, hookTolH: target.BaseTitanCache.Head.lossyScale.z / 2f);
                    _controller.FlightAround(new Vector3(mouthPos.x, (napePos.y + mouthPos.y) * 0.5f, mouthPos.z), radius, safeRadius, hookTolH: target.BaseTitanCache.Head.lossyScale.z / 2f);
                }
                else
                {
                    _controller.Jump();
                }

                if (_human.IsHookedAny())
                {
                    AttackEnemy(isDirectlySeeingTarget, tryHold: true, delayAttack: true);
                }
                else
                {
                    _isAttacked = false;
                }
                // var isAttacking = _isAttacked || _human.State == HumanState.Attack || _human.State == HumanState.SpecialAttack;

                return this;
            }
        }
    }
}