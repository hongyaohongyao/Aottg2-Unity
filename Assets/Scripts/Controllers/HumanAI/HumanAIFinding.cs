
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIFinding : FSMState
        {
            protected HumanAIController _controller;
            protected Human _human;
            protected FSMState lastAction = null;
            protected Vector3? tempTargetPosition = null;

            protected float tempTargetTimer;

            public HumanAIFinding(FSM fsm, HumanAIController controller) : base(fsm)
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
                tempTargetTimer = 0;
                tempTargetPosition = null;
            }


            public override FSMState StateAction()
            {
                if (tempTargetTimer > 0)
                    tempTargetTimer -= Time.deltaTime;
                if (!_controller.IsTargetValid() || _controller.TargetDirection.magnitude < _controller.LockingDistance)
                {
                    return FSM.DefaultState;
                }
                if (NeedFindTempTarget())
                {
                    FindTempTarget();
                    if (tempTargetPosition != null)
                    {
                        // Maintain flying altitude
                        var p = (Vector3)tempTargetPosition;
                        p.y += Mathf.Max(20f, _controller.TargetDirection.y);
                    }
                }
                if (tempTargetPosition != null)
                {
                    _controller.StraightFlight((Vector3)tempTargetPosition, 30f);
                }
                return this;
            }
            public bool NeedFindTempTarget()
            {
                if (tempTargetPosition == null || tempTargetTimer <= 0 || Vector3.Distance(_human.Cache.Transform.position, (Vector3)tempTargetPosition) < 10.0)
                {
                    return true;
                }
                var humanPosition = _human.Cache.Transform.position;
                var targetPosition = _controller.TargetPosition;
                var tempDirectionH = (Vector3)tempTargetPosition - humanPosition;
                tempDirectionH.y = 0;
                if (Vector3.Angle(tempDirectionH, new Vector3(targetPosition.x, 0f, targetPosition.z)) > 100)
                {
                    return true;
                }
                return false;
            }

            public void FindTempTarget()
            {
                var humanPosition = _human.Cache.Transform.position;
                var targetPosition = _controller.TargetPosition;
                var targetDirection = _controller.TargetDirection;
                var start = humanPosition + targetDirection.normalized;
                var end = humanPosition + targetDirection + targetDirection.normalized * 10.0f;
                if (Physics.Linecast(start, end, out RaycastHit result, HumanAIController.BarrierMask))
                {
                    var point = result.point;
                    var target2Point = point - targetPosition;
                    var self2Point = point - humanPosition;
                    if (target2Point.magnitude <= 5.0f)
                    {
                        // Move to target;
                        tempTargetPosition = (targetPosition + humanPosition) * 0.5f;
                        return;
                    }
                    else if (self2Point.magnitude <= 90)
                    {
                        // Adjust the body position.
                        var moveDirection = new Vector3(targetDirection.x, Mathf.Max(0.0f, targetDirection.y), targetDirection.z).normalized;
                        var directionQuaternion = Quaternion.LookRotation(new Vector3(_controller.TargetDirection.x, 0.0f, _controller.TargetDirection.z));
                        var directionLeft80 = (directionQuaternion * HumanAIController.VectorLeft80).normalized;
                        var isHit = _controller.DetectDirection(directionLeft80, 5f, 5f, out result);
                        var maxDistance = Mathf.Infinity;
                        Vector3? bestDirection = null;
                        if (!isHit || (result.distance + 5f >= 40f))
                        {
                            tempTargetPosition = humanPosition + 35f * directionLeft80;
                            return;
                        }
                        else if (isHit && result.distance < maxDistance)
                        {
                            maxDistance = result.distance;
                            bestDirection = directionLeft80;
                        }
                        var directionRight80 = (directionQuaternion * HumanAIController.VectorRight80).normalized;
                        isHit = _controller.DetectDirection(directionRight80, 5f, 5f, out result);
                        if (!isHit || (result.distance + 5f >= 40f))
                        {
                            tempTargetPosition = humanPosition + 35f * directionRight80;
                            return;
                        }
                        else if (isHit && result.distance < maxDistance)
                        {
                            maxDistance = result.distance;
                            bestDirection = directionRight80;
                        }
                        var directionUp80 = (directionQuaternion * HumanAIController.VectorUp80).normalized;
                        isHit = _controller.DetectDirection(directionUp80, 5f, 5f, out result);
                        if (!isHit || (result.distance + 5f >= 40f))
                        {
                            tempTargetPosition = humanPosition + 35f * directionUp80;
                            return;
                        }
                        else if (isHit && result.distance < maxDistance)
                        {
                            maxDistance = result.distance;
                            bestDirection = directionUp80;
                        }
                        if (bestDirection is Vector3 v)
                        {
                            _controller.Dodge(v);
                        }
                    }
                    else
                    {
                        // Move Closest;
                        tempTargetPosition = (point + humanPosition) * 0.5f;
                        return;
                    }
                }
                tempTargetPosition = null;
            }
        }
    }
}