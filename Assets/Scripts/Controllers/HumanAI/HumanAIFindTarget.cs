
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIFindTarget : HumanAIAutomatonState
        {

            public HumanAIFindTarget(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override AutomationState StateAction()
            {
                if (!_controller.IsTargetValid())
                {
                    _controller.Target = _controller.FindNearestEnemy();
                }
                return Automation.DefaultState;
            }
        }
    }
}