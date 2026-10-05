using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LSFRevitPlugin.Domain.Models;
using LSFRevitPlugin.Geometry;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                Reference referenciaSelecionada = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new FiltroSomenteParedes(),
                    "Selecione uma parede reta para gerar a estrutura.");

                Wall parede = doc.GetElement(referenciaSelecionada) as Wall;

                LocationCurve curvaParede = parede?.Location as LocationCurve;
                Line linhaParede = curvaParede?.Curve as Line;
                if (linhaParede == null)
                {
                    TaskDialog.Show("LSF Modeler", "Selecione uma parede reta (paredes curvas ainda nao sao suportadas).");
                    return Result.Cancelled;
                }

                XYZ inicioParede = linhaParede.GetEndPoint(0);
                XYZ direcaoParede = (linhaParede.GetEndPoint(1) - inicioParede).Normalize();

                double? alturaParedeInterna = parede.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble();
                double alturaParedeCm = alturaParedeInterna.HasValue
                    ? RevitVersionAdapter.InternoParaCentimetros(alturaParedeInterna.Value)
                    : 0;

                // Os vaos sao detectados aqui, antes da janela, para o usuario poder ajustar as
                // medidas de cada um. A deteccao automatica e o ponto de partida; os ajustes
                // feitos na janela valem so para esta geracao.
                List<OpeningInfo> vaos = new OpeningReinforcement()
                    .DetectarVaos(doc, parede)
                    .OrderBy(v => (v.PontoCentral - inicioParede).DotProduct(direcaoParede))
                    .ToList();

                var janelaOpcoes = new OpcoesGeracaoWindow(vaos, inicioParede, direcaoParede, alturaParedeCm);
                bool? resultadoJanela = janelaOpcoes.ShowDialog();

                if (resultadoJanela != true)
                {
                    return Result.Cancelled;
                }

                double espacamentoCm = janelaOpcoes.EspacamentoEscolhidoCm;
                PerfilSpec perfilEscolhido = janelaOpcoes.PerfilEscolhido;

                var servico = new StructureGenerationService();
                ResultadoGeracao r = servico.GerarEstrutura(doc, parede, espacamentoCm, perfilEscolhido, vaos);

                string aviso = r.VaoExcedeuLimite
                    ? $"\n\nATENCAO: maior vao entre montantes = {r.MaiorVaoCm:F1}cm (acima do limite de {espacamentoCm:F0}cm)."
                    : $"\n\nMaior vao entre montantes: {r.MaiorVaoCm:F1}cm (dentro do limite de {espacamentoCm:F0}cm).";

                string ajustados = janelaOpcoes.QuantidadeVaosAjustados > 0
                    ? $", {janelaOpcoes.QuantidadeVaosAjustados} com medidas ajustadas"
                    : "";
                string infoVaos = $"\n\nVaos (portas/janelas) detectados: {r.VaosDetectados}{ajustados} ({r.VaosJaReforcados} ja reforcados, {r.ElementosDeVao} elementos de reforco criados).";

                string notaPortas = vaos.Any(v => v.Categoria == "Porta")
                    ? " A guia inferior foi interrompida nos vaos de porta."
                    : "";
                string infoGuias = $"\n\nGuias geradas: {r.GuiasCriadas} (superior e inferior, largura {perfilEscolhido.LarguraAlmaMm:F0}mm).{notaPortas}";

                string infoPerfil = $"\n\nPerfil selecionado: {perfilEscolhido.DescricaoResumo()} (aplicado aos montantes e as guias).";

                // Paineis da parede (gravados pelo botao "Painelizar").
                bool painelizacaoDesatualizada;
                List<PainelInfo> paineis = PainelizacaoService.ObterPaineis(parede, out painelizacaoDesatualizada);
                string infoPaineis;
                if (painelizacaoDesatualizada)
                {
                    infoPaineis = "\n\nPaineis: a painelizacao gravada nesta parede esta desatualizada (o comprimento da parede mudou) e foi ignorada. Use o botao Painelizar para refazer.";
                }
                else if (paineis.Count == 1)
                {
                    infoPaineis = "\n\nPaineis: parede inteira como painel unico.";
                }
                else
                {
                    string lista = string.Join(
                        "\n",
                        paineis.Select(p =>
                            $"  Painel {p.Numero}: de {RevitVersionAdapter.InternoParaCentimetros(p.InicioInterno):F1} a {RevitVersionAdapter.InternoParaCentimetros(p.FimInterno):F1} cm"));
                    infoPaineis = $"\n\nPaineis: {paineis.Count}\n{lista}";
                }

                TaskDialog.Show(
                    "LSF Modeler",
                    $"{r.TotalCriados} montantes gerados ({r.MontantesPrincipais} principais + {r.MontantesH} do H ({r.EncontrosL} L, {r.HJaExistentes} ja existentes) + {r.MontantesTriplo} do Triplo ({r.EncontrosT} T, {r.TriploJaExistentes} ja existentes) + {r.MontantesQuadruplo} do Quadruplo ({r.EncontrosCruz} +, {r.QuadruploJaExistentes} ja existentes), {r.Reposicionados} pulado(s) por colisao, {r.IsoladosPuladosPorT} isolado(s) pulado(s) por T){infoVaos}{infoGuias}{infoPerfil}{infoPaineis}{aviso}");

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
    }

    public class FiltroSomenteParedes : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is Wall;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}