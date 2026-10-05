using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LSFRevitPlugin.Services;
using LSFRevitPlugin.UI;

namespace LSFRevitPlugin.Core
{
    /// <summary>
    /// Botao "Quantitativo": le a estrutura e as placas que ja estao no projeto e mostra o resultado
    /// numa janela. Na janela, o usuario pode gravar um CSV (na pasta do projeto, ou em Documentos se
    /// o projeto ainda nao foi salvo) ou criar os parafusos no 3D.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class QuantitativoCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                DadosQuantitativo dados = QuantitativoService.Calcular(doc);

                var janela = new QuantitativoWindow(dados, uiapp.MainWindowHandle);
                janela.ShowDialog();

                if (janela.CriarParafusos3D)
                {
                    MostrarResultadoDosParafusos3D(doc, dados);
                    return Result.Succeeded;
                }

                if (!janela.GerarCsv) return Result.Succeeded;

                string pasta = !string.IsNullOrEmpty(doc.PathName)
                    ? Path.GetDirectoryName(doc.PathName)
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string caminho = QuantitativoService.GravarCsv(dados, pasta);

                try
                {
                    Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
                }
                catch (Exception)
                {
                    // Sem programa associado ao CSV: o arquivo continua na pasta.
                }

                var texto = new StringBuilder();
                texto.AppendLine("Quantitativo gravado em:");
                texto.AppendLine(caminho);
                texto.AppendLine();
                texto.AppendLine(dados.ElementosEstrutura + " elementos de estrutura, "
                    + dados.ComprimentoTotalM.ToString("0.0") + " m de perfil.");
                texto.AppendLine(dados.PecasDePlaca + " pecas de placa, " + dados.PlacasUsadas
                    + " placas usadas, " + dados.Recortes + " recortes.");
                texto.AppendLine(dados.ParafusosEstrutura + " parafusos na estrutura e "
                    + dados.ParafusosPlacas + " nas placas.");
                texto.AppendLine();
                texto.AppendLine(dados.Observacao);

                foreach (string aviso in dados.Avisos)
                {
                    texto.AppendLine("- " + aviso);
                }

                TaskDialog.Show("LSF Modeler - Quantitativo", texto.ToString().TrimEnd());
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("LSF Modeler - Erro", ex.Message);
                return Result.Failed;
            }
        }

        private static void MostrarResultadoDosParafusos3D(Document doc, DadosQuantitativo dados)
        {
            var texto = new StringBuilder();

            if (dados.Parafusos == null || dados.Parafusos.Parafusos.Count == 0)
            {
                texto.AppendLine("O calculo nao encontrou nenhum parafuso para criar no 3D.");
                texto.AppendLine("Confira, na janela do Quantitativo, as abas Parafusos e Estrutura.");

                foreach (string aviso in dados.Avisos)
                {
                    texto.AppendLine("- " + aviso);
                }

                TaskDialog.Show("LSF Modeler - Parafusos", texto.ToString().TrimEnd());
                return;
            }

            ResultadoCriacao3D r = ParafusosService.Criar3D(doc, dados.Parafusos);

            texto.AppendLine(r.Criados + " de " + dados.Parafusos.Parafusos.Count + " parafusos criados no 3D em "
                + r.Elementos + " elementos ("
                + dados.ParafusosEstrutura + " da estrutura e " + dados.ParafusosPlacas + " das placas).");

            if (r.Apagados > 0)
            {
                texto.AppendLine(r.Apagados + " elementos de parafusos antigos foram substituidos.");
            }

            if (r.Falhas > 0)
            {
                texto.AppendLine();
                texto.AppendLine(r.Falhas + " parafusos nao puderam ser criados. Primeiro erro: " + r.PrimeiroErro);
            }

            texto.AppendLine();
            texto.AppendLine("Eles ficam na categoria Modelo generico, em elementos chamados \"LSF - Parafusos\" "
                + "(estrutura em vermelho, placas em preto).");
            texto.AppendLine("Se voce refizer a estrutura ou as placas, abra o Quantitativo e crie os parafusos de novo: "
                + "os anteriores sao substituidos.");

            TaskDialog.Show("LSF Modeler - Parafusos", texto.ToString().TrimEnd());
        }
    }
}