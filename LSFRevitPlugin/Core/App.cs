using Autodesk.Revit.UI;
using System.Reflection;

namespace LSFRevitPlugin.Core
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "LSF Modeler";
            application.CreateRibbonTab(tabName);

            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Estrutura");

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "LSFModelerCommand",
                "Ativar\nLSF",
                assemblyPath,
                "LSFRevitPlugin.Core.Command");

            buttonData.ToolTip = "Gera a estrutura de uma parede reta: escolha o sistema (Drywall ou Steel Frame), o perfil, o espacamento e ajuste os vaos. Cria montantes, guias, reforcos de vao e dos encontros L, T e +.";

            panel.AddItem(buttonData);

            PushButtonData painelizarData = new PushButtonData(
                "LSFModelerPainelizar",
                "Painelizar\nparedes",
                assemblyPath,
                "LSFRevitPlugin.Core.PainelizarCommand");

            painelizarData.ToolTip = "Divide uma parede em paineis: voce clica nos pontos onde cada painel termina.";

            panel.AddItem(painelizarData);

            PushButtonData placasData = new PushButtonData(
                "LSFModelerPlacas",
                "Placas",
                assemblyPath,
                "LSFRevitPlugin.Core.PlacasCommand");

            placasData.ToolTip = "Placas de fechamento: escolha tipo, medida e orientacao por painel e por face e refaca so os paineis marcados.";

            panel.AddItem(placasData);

            PushButtonData quantitativoData = new PushButtonData(
                "LSFModelerQuantitativo",
                "Quantitativo",
                assemblyPath,
                "LSFRevitPlugin.Core.QuantitativoCommand");

            quantitativoData.ToolTip = "Mostra o quantitativo do projeto (estrutura, placas, recortes e parafusos) numa janela. Dali voce grava um CSV (abre no Excel) ou cria os parafusos no 3D.";

            panel.AddItem(quantitativoData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}