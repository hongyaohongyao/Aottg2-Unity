
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIFollowing : FSMState
        {
            protected HumanAIController _controller;
            protected Human _human;

            protected HumanAIFollowingStates state;
            protected Vector3? groundingPosition = null;

            protected float _hookedTimer = 0.0f;

            public HumanAIFollowing(FSM fsm, HumanAIController controller) : base(fsm)
            {
                SetController(controller);
            }

            public void SetController(HumanAIController controller)
            {
                _controller = controller;
                _human = controller.Human;
            }

            public override void StateEnd()
            {
                _hookedTimer = 0f;
                state = HumanAIFollowingStates.None;
                groundingPosition = null;
            }

            public override FSMState StateAction()
            {
                var Target = _controller.Target;
                if (!_controller.IsTargetValid())
                {
                    return FSM.DefaultState;
                }
                if (Vector3.Distance(_human.transform.position, _controller.TargetPosition) > _controller.LockingDistance)
                {
                    return FSM.GetState((int)HumanAIStates.Finding);
                }
                var targetDirection = _controller.TargetDirection;
                var targetDirectionH = new Vector3(targetDirection.x, 0f, targetDirection.z);
                var targetPosition = _controller.TargetPosition;
                var humanPosition = _human.transform.position;

                var inRange = targetDirectionH.magnitude < 20f && Mathf.Abs(targetDirection.y) < 20f;
                if (state == HumanAIFollowingStates.Found)
                {
                    if (!inRange)
                    {
                        state = HumanAIFollowingStates.None;
                        groundingPosition = null;
                    }
                    _controller.ReleaseHookAll();
                    return this;
                }
                else if (inRange)
                {
                    if (targetDirection.y > 5f)
                    {
                        if (_human.IsHookedAny())
                        {
                            _controller.ReleaseHookAll();
                        }
                        else
                        {
                            _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 30f);
                        }
                        return this;
                    }
                    else if (_human.Grounded)
                    {
                        groundingPosition = null;
                        state = HumanAIFollowingStates.Found;
                        return this;
                    }
                    else if (groundingPosition is Vector3 gp)
                    {
                        // Game.Print("ToGround " + self.ToGround);
                        if (gp.y > humanPosition.y)
                        {
                            groundingPosition = null;
                            return this;
                        }
                        if (_human.IsHookedAny())
                        {
                            var hookPosition = _controller.GetHookPosition();
                            if (Vector3.Distance(hookPosition, gp) > 2.0)
                            {
                                _controller.ReleaseHookAll();
                                groundingPosition = null;
                            }
                            _controller.ReelIn();
                        }
                        else
                        {
                            groundingPosition = null;
                        }
                        return this;
                    }
                    if (Physics.Linecast(humanPosition + new Vector3(0f, -0.1f, 0f), humanPosition + new Vector3(0f, -20f, 0f), out RaycastHit groundResult, HumanAIController.BarrierMask))
                    {
                        if (groundingPosition == null)
                        {
                            _controller.LaunchHook(groundResult.point);
                            groundingPosition = groundResult.point;
                        }
                        return this;
                    }
                    else
                    {
                        if (_human.IsHookedAny())
                        {
                            var hookTargetL = _controller.IsHookedTarget(_human.HookLeft);
                            var hookTargetR = _controller.IsHookedTarget(_human.HookRight);
                            var hookTarget = hookTargetL || hookTargetR;
                            var hookL = _human.IsHookedLeft();
                            var hookR = _human.IsHookedRight();
                            // Game.Print("hastarget " + hookTarget);
                            var reelIn = false;
                            if (hookL)
                            {
                                if (hookTargetL)
                                {
                                    reelIn = true;
                                }
                                else
                                {
                                    _controller.ReleaseHookLeft();
                                }
                            }
                            if (hookR)
                            {
                                if (hookTargetR)
                                {
                                    reelIn = true;
                                }
                                else
                                {
                                    _controller.ReleaseHookRight();
                                }
                            }
                            if (reelIn)
                            {
                                _hookedTimer = 3.0f;
                                _controller.ReelIn();
                            }
                            else
                            {
                                _hookedTimer = 0.0f;
                            }

                            if (Vector3.Distance(humanPosition, targetPosition) < 1.5)
                            {
                                groundingPosition = null;
                                state = HumanAIFollowingStates.Found;
                            }

                            if (_hookedTimer <= 0f)
                            {
                                _controller.ReleaseHookAll();
                            }
                        }
                        else if (!_human.IsHookingAny() && _human.IsHookReady())
                        {
                            var start = humanPosition + targetDirection.normalized * 2f;
                            var end = humanPosition + targetDirection.normalized * _controller.LockingDistance;
                            if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                            {
                                var target2PointDistance = Vector3.Distance(result.point, targetPosition);
                                if (target2PointDistance < 1f)
                                {
                                    if (Target is BaseCharacter character)
                                    {
                                        targetPosition = HumanAIController.CorrectHookPosition(humanPosition, targetPosition, character.Cache.Rigidbody.velocity, _controller.HookSpeed);
                                    }
                                    _controller.LaunchHook(targetPosition);
                                }
                            }
                        }
                    }
                    // Game.Print("Keep Stay");
                    return this;
                }
                _controller.StraightFlight(humanPosition + new Vector3(0f, 5f, 0f) + targetDirection * 0.95f, 20f);
                return this;
            }
        }

        enum HumanAIFollowingStates
        {
            None,
            Found
        }
    }
}