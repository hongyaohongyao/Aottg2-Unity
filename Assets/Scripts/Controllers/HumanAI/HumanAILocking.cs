
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAILocking : AutomationState
        {
            protected HumanAIController _controller;
            protected Human _human;

            public HumanAILocking(Automaton automaton, HumanAIController controller) : base(automaton)
            {
                SetController(controller);
            }

            public void SetController(HumanAIController controller)
            {
                _controller = controller;
                _human = controller.Human;
            }

            public override AutomationState StateAction()
            {
                var Target = _controller.Target;
                if (!_controller.IsTargetValid())
                {
                    return Automation.DefaultState;
                }
                if (Vector3.Distance(_human.transform.position, _controller.TargetPosition) > _controller.LockingDistance)
                {
                    return Automation.GetState((int)HumanAIStates.Finding);
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
                    // result = self.Core.LineCast(self.Core.Transform.Position + Vector3(0,0.1,0), self.Core.Transform.Position + Vector3(0.0,-10.0,0.0));
                    // reelInCond = self.Skills.Pivot && (result == null || (result.Distance > 7.0 || (result.Distance > 2.0 && self.Core.Rigidbody.GetVelocity().Y > 0)));
                    var reelInCond = _human.Pivot;
                    if (reelInCond)
                    {
                        _controller.ReelIn();
                    }
                    else
                    {
                        _controller.RandomJump(false, -1.0f);
                    }

                    if (targetDirection.magnitude <= 5.0f)
                    {
                        _controller.Attack();
                    }
                    return this;
                }
                _controller.StraightFlight(targetPosition + new Vector3(0f, 5f, 0f), 30f);
                var correctPosition = targetPosition;
                correctPosition = HumanAIController.CorrectHookPosition(humanPosition, correctPosition, target.Cache.Rigidbody.velocity, _controller.HookSpeed);
                if (_human.HookRight.HookReady())
                {
                    _controller.LaunchHookRight(correctPosition);
                }
                return this;
            }

            public AutomationState LockingTitan(BaseTitan target)
            {
                return Automation.DefaultState;
            }
        }
    }
}