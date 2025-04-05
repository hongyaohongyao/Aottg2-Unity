
using UnityEngine;
using Characters;
using Utility;
using Unity.Mathematics;

namespace Controllers
{
    namespace HumanAIActions
    {
        class HumanAIBattle : HumanAIAutomatonState
        {

            public HumanAIBattle(Automaton automaton, HumanAIController controller) : base(automaton, controller)
            {
            }

            public override void StateEnd()
            {
            }


            public override AutomationState StateAction()
            {
                if (!_controller.IsTargetValid())
                {
                    return Automation.GetState(HumanAIStates.FindTarget);
                }
                return Automation.GetState(HumanAIStates.Locking);
            }

        }
    }
}