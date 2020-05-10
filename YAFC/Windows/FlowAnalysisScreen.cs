using System.Collections.Generic;
using System.Numerics;
using YAFC.Model;
using YAFC.UI;

namespace YAFC
{
    public class FlowAnalysisScreen : PseudoScreen
    {
        private static readonly FlowAnalysisScreen Instance = new FlowAnalysisScreen();
        private readonly VirtualScrollList<(Recipe recipe, float amount, int cluster)> list;

        public FlowAnalysisScreen()
        {
            list = new VirtualScrollList<(Recipe recipe, float amount, int cluster)>(40f, new Vector2(float.PositiveInfinity, 2f), Drawer);
        }

        private void Drawer(ImGui gui, (Recipe recipe, float amount, int cluster) element, int index)
        {
            using (gui.EnterRow())
            {
                gui.BuildFactorioObjectIcon(element.recipe);
                gui.RemainingRow().BuildText(element.cluster+" " + element.amount + " " + element.recipe.locName);
            }

            gui.BuildFactorioObjectButton(gui.lastRect, element.recipe);
        }

        public static void Show(Goods target, List<(Recipe recipe, float amount, int cluster)> recipes)
        {
            Instance.list.data = recipes;
            MainScreen.Instance.ShowPseudoScreen(Instance);
        }
        
        public override void Build(ImGui gui)
        {
            BuildHeader(gui, "Flow analysis");
            list.Build(gui);
        }
    }
}