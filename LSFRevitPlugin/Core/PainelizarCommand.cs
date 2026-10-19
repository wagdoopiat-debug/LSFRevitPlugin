using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Services;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Botao "Painelizar": o usuario seleciona uma parede reta e clica nos pontos onde cada
    /// painel termina. Os pontos ficam gravados na parede. Este comando ainda nao altera a
    /// estrutura ja gerada -- a geracao por painel e o passo seguinte.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PainelizarCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                Reference referencia = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new FiltroSomenteParedes(),
                    "Selecione a parede que sera painelizada.");

                Wall parede = doc.GetElement(referencia) as Wall;

                LocationCurve curvaParede = parede != null ? parede.Location as LocationCurve : null;
                Line linhaParede = curvaParede != null ? curvaParede.Curve as Line : null;
                if (linhaParede == null)
                {
                    TaskDialog.Show("LSF Modeler", "Selecione uma parede reta (paredes curvas ainda nao sao suportadas).");
                    return Result.Cancelled;
                }

                XYZ inicio = linhaParede.GetEndPoint(0);
                XYZ direcao = (linhaParede.GetEndPoint(1) - inicio).Normalize();
                double comprimento = linhaParede.Length;
                double larguraMinima = RevitVersionAdapter.CentimetrosParaInterno(
                    PainelizacaoService.LarguraMinimaPainelCm);

                // Parede que ja tem painelizacao: perguntar o que fazer.
                bool desatualizada;
                List<double> pontosExistentes = PainelizacaoService.LerPontosCorte(parede, out desatualizada);

                if (pontosExistentes.Count > 0 || desatualizada)
                {
                    var dialogo = new TaskDialog("LSF Modeler - Painelizar");
                    dialogo.MainInstruction = desatualizada
                        ? "A painelizacao gravada nesta parede esta desatualizada."
                        : "Esta parede ja esta painelizada.";
                    dialogo.MainContent = desatualizada
                        ? "O comprimento da parede mudou desde que ela foi painelizada, entao os pontos antigos nao valem mais."
                        : DescreverPaineis(PainelizacaoService.MontarPaineis(pontosExistentes, comprimento));
                    dialogo.AddCommandLink(
                        TaskDialogCommandLinkId.CommandLink1,
                        "Redefinir os paineis",
                        "Voce clica os novos pontos; os pontos atuais so sao trocados se voce definir ao menos um.");
                    dialogo.AddCommandLink(
                        TaskDialogCommandLinkId.CommandLink2,
                        "Remover a painelizacao",
                        "A parede volta a ser um painel unico.");
                    dialogo.CommonButtons = TaskDialogCommonButtons.Cancel;

                    TaskDialogResult escolha = dialogo.Show();

                    if (escolha == TaskDialogResult.CommandLink2)
                    {
                        using (Transaction t = new Transaction(doc, "LSF - Remover paineis da parede"))
                        {
                            t.Start();
                            PainelizacaoService.Remover(parede);
                            t.Commit();
                        }

                        TaskDialog.Show("LSF Modeler", "Painelizacao removida. A parede voltou a ser um painel unico.");
                        return Result.Succeeded;
                    }

                    if (escolha != TaskDialogResult.CommandLink1)
                    {
                        return Result.Cancelled;
                    }
                }

                // Clique dos pontos onde cada painel termina.
                const ObjectSnapTypes snaps = ObjectSnapTypes.Endpoints
                    | ObjectSnapTypes.Midpoints
                    | ObjectSnapTypes.Nearest
                    | ObjectSnapTypes.Intersections;

                var pontos = new List<double>();
                string aviso = "";

                while (true)
                {
                    string prompt = "Painel " + (pontos.Count + 1)
                        + ": clique onde ele termina (o ultimo painel vai ate o fim da parede). ESC conclui."
                        + aviso;

                    XYZ clicado;
                    try
                    {
                        clicado = uiDoc.Selection.PickPoint(snaps, prompt);
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }

                    double distancia = (clicado - inicio).DotProduct(direcao);
                    aviso = "";

                    if (distancia < larguraMinima || distancia > comprimento - larguraMinima)
                    {
                        aviso = " [ponto ignorado: muito perto de uma ponta da parede]";
                        continue;
                    }

                    if (pontos.Any(p => Math.Abs(p - distancia) < larguraMinima))
                    {
                        aviso = " [ponto ignorado: muito perto de outro ponto]";
                        continue;
                    }

                    pontos.Add(distancia);
                    pontos.Sort();
                    aviso = " [ultimo ponto a "
                        + RevitVersionAdapter.InternoParaCentimetros(distancia).ToString("F1")
                        + " cm da ponta inicial]";
                }

                if (pontos.Count == 0)
                {
                    return Result.Cancelled;
                }

                using (Transaction t = new Transaction(doc, "LSF - Definir paineis da parede"))
                {
                    t.Start();
                    PainelizacaoService.Gravar(parede, pontos);
                    t.Commit();
                }

                List<PainelInfo> paineis = PainelizacaoService.MontarPaineis(pontos, comprimento);
                TaskDialog.Show(
                    "LSF Modeler - Painelizar",
                    "Parede dividida em " + paineis.Count + " paineis:\n\n"
                    + DescreverPaineis(paineis)
                    + "\n\nO painel 1 comeca na ponta inicial da parede (onde a linha de localizacao comecou)."
                    + "\nA estrutura ja gerada nao muda -- a geracao por painel e o proximo passo.");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("LSF Modeler - Erro", ex.Message);
                return Result.Failed;
            }
        }

        private static string DescreverPaineis(List<PainelInfo> paineis)
        {
            var texto = new StringBuilder();
            foreach (PainelInfo p in paineis)
            {
                double inicioCm = RevitVersionAdapter.InternoParaCentimetros(p.InicioInterno);
                double fimCm = RevitVersionAdapter.InternoParaCentimetros(p.FimInterno);
                double larguraCm = RevitVersionAdapter.InternoParaCentimetros(p.LarguraInterna);

                texto.AppendLine(
                    "Painel " + p.Numero + ": de " + inicioCm.ToString("F1")
                    + " a " + fimCm.ToString("F1") + " cm (largura " + larguraCm.ToString("F1") + " cm)");
            }

            return texto.ToString().TrimEnd();
        }
    }
}