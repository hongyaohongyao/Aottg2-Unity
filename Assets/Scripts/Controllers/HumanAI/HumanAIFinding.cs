
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIFinding : HumanAIAutomatonState
        {
            protected AutomationState lastAction = null;
            protected Vector3? tempTargetPosition = null;

            protected float tempTargetTimer;

            public HumanAIFinding(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override void StateEnd()
            {
                tempTargetTimer = 0;
                tempTargetPosition = null;
            }


            public override AutomationState StateAction()
            {
                if (tempTargetTimer > 0)
                    tempTargetTimer -= Time.deltaTime;
                if (!_controller.IsTargetValid() || _controller.TargetDirection.magnitude < _controller.LockingDistance)
                {
                    return Automation.DefaultState;
                }

                FindTempTarget();
                if (tempTargetPosition is Vector3 p)
                {
                    // Maintain flying altitude
                    p.y += Mathf.Max(20f, _controller.TargetDirection.y);
                    _controller.StraightFlight(p, 30f);
                }
                return this;
            }
            // public bool NeedFindTempTarget()
            // {
            //     if (tempTargetPosition == null || tempTargetTimer <= 0 || Vector3.Distance(_human.Cache.Transform.position, (Vector3)tempTargetPosition) < 10.0)
            //     {
            //         return true;
            //     }
            //     var humanPosition = _human.Cache.Transform.position;
            //     var targetPosition = _controller.TargetPosition;
            //     var tempDirectionH = (Vector3)tempTargetPosition - humanPosition;
            //     tempDirectionH.y = 0;
            //     if (Vector3.Angle(tempDirectionH, new Vector3(targetPosition.x, 0f, targetPosition.z)) > 100)
            //     {
            //         return true;
            //     }
            //     return false;
            // }

            public void FindTempTarget()
            {
                var humanPosition = _human.Cache.Transform.position;
                var targetDirection = _controller.TargetDirection;
                var start = humanPosition + targetDirection.normalized;
                var end = humanPosition + targetDirection + targetDirection.normalized * 10f;
                if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                {
                    tempTargetPosition = _controller.FindTempTarget(result, 5f, targetDirection.magnitude * 0.5f) ?? (humanPosition + targetDirection * 0.5f);
                }
                else
                {
                    tempTargetPosition = null;
                }
            }
        }
    }
}