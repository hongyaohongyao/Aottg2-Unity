
using UnityEngine;
using Characters;
using Utility;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIFollowing : HumanAIAutomatonState
        {
            protected bool _isFound;
            public HumanAIFollowing(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override void StateStart()
            {
                _isFound = false;
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

                var humanPosition = _human.transform.position;
                var targetPosition = _controller.Target.GetPosition();
                var targetDirection = _controller.Target.GetPosition() - humanPosition;
                targetPosition -= new Vector3(targetDirection.x, 0f, targetDirection.z).normalized * 2f;
                targetDirection = targetPosition - humanPosition;
                var targetDirectionH = new Vector3(targetDirection.x, 0f, targetDirection.z);
                if (targetDirectionH.magnitude < 5f)
                {
                    if (_isFound)
                    {
                        return this;
                    }
                    else if (_human.Grounded)
                    {
                        _controller.ReleaseHookAll();
                        _isFound = true;
                        return this;
                    }
                    if (targetDirection.y < 0f)
                        _controller.ReleaseHookAll();
                }
                _isFound = false;

                if (!_human.Grounded)
                {
                    _controller.Jump();
                }
                var hookedTargetL = _controller.IsHookedTarget(_human.HookLeft, fuzzy: true, distannse2TargetTol: 2f);
                var hookedTargetR = _controller.IsHookedTarget(_human.HookRight, fuzzy: true, distannse2TargetTol: 2f);
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
                    return this;
                }
                var start = humanPosition + targetDirection.normalized;
                var end = humanPosition + targetDirection.normalized * 120f;
                if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                {
                    if (_human.HookRight.HookReady())
                    {
                        _controller.LaunchHookRight(targetPosition);
                    }
                    _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 60f);
                }
                else
                {
                    _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 60f);
                }
                return this;
            }
        }
    }
}