using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Guarda e le a painelizacao de uma parede. Os pontos de corte ficam gravados na propria
    /// parede (Extensible Storage), como distancias ao longo do eixo a partir da ponta inicial,
    /// entao sobrevivem a salvar/reabrir o projeto.
    /// </summary>
    public static class PainelizacaoService
    {
        /// <summary>
        /// Largura minima aceita para um painel, em cm. Pontos mais perto que isso de uma ponta
        /// da parede ou de outro ponto sao ignorados na hora de painelizar.
        /// </summary>
        public const double LarguraMinimaPainelCm = 20.0;

        private static readonly Guid SchemaGuid = new Guid("7c1f3a52-9d4e-4b8a-a6f0-2e5d8b91c3a7");
        private const string NomeSchema = "LSFPainelizacaoParede";
        private const string CampoPontos = "PontosCorteCm";
        private const string CampoComprimento = "ComprimentoCm";

        // Diferenca de comprimento (cm) a partir da qual a painelizacao gravada e considerada
        // desatualizada (a parede foi esticada, encurtada ou trocada).
        private const double ToleranciaComprimentoCm = 0.5;

        private static Schema ObterOuCriarSchema()
        {
            Schema existente = Schema.Lookup(SchemaGuid);
            if (existente != null) return existente;

            var builder = new SchemaBuilder(SchemaGuid);
            builder.SetSchemaName(NomeSchema);
            builder.SetVendorId("LSFModeler");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(CampoPontos, typeof(string));
            builder.AddSimpleField(CampoComprimento, typeof(string));
            return builder.Finish();
        }

        private static double ComprimentoDaParede(Wall parede)
        {
            LocationCurve curva = parede.Location as LocationCurve;
            return curva != null ? curva.Curve.Length : 0.0;
        }

        private static string FormatarCm(double valorCm)
        {
            return valorCm.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static List<double> LerListaCm(string texto)
        {
            var lista = new List<double>();
            if (string.IsNullOrWhiteSpace(texto)) return lista;

            foreach (string parte in texto.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                double valor;
                if (double.TryParse(parte, NumberStyles.Float, CultureInfo.InvariantCulture, out valor))
                {
                    lista.Add(valor);
                }
            }

            return lista;
        }

        /// <summary>
        /// Grava os pontos de corte na parede. Precisa ser chamado com uma Transaction aberta.
        /// Os pontos sao distancias a partir da ponta inicial da parede, em unidades internas.
        /// </summary>
        public static void Gravar(Wall parede, IList<double> pontosCorteInterno)
        {
            Schema schema = ObterOuCriarSchema();

            string pontos = string.Join(
                ";",
                pontosCorteInterno
                    .OrderBy(p => p)
                    .Select(p => FormatarCm(RevitVersionAdapter.InternoParaCentimetros(p))));

            string comprimento = FormatarCm(
                RevitVersionAdapter.InternoParaCentimetros(ComprimentoDaParede(parede)));

            var entidade = new Entity(schema);
            entidade.Set<string>(CampoPontos, pontos);
            entidade.Set<string>(CampoComprimento, comprimento);
            parede.SetEntity(entidade);
        }

        /// <summary>
        /// Remove a painelizacao da parede (ela volta a ser um painel unico). Precisa ser
        /// chamado com uma Transaction aberta.
        /// </summary>
        public static void Remover(Wall parede)
        {
            Schema schema = ObterOuCriarSchema();
            parede.DeleteEntity(schema);
        }

        /// <summary>
        /// Le os pontos de corte gravados na parede, em unidades internas, ordenados. Se a
        /// parede nao tem painelizacao, devolve lista vazia. Se o comprimento da parede mudou
        /// desde a gravacao, devolve lista vazia e marca desatualizada = true.
        /// </summary>
        public static List<double> LerPontosCorte(Wall parede, out bool desatualizada)
        {
            desatualizada = false;
            var pontos = new List<double>();

            Schema schema = ObterOuCriarSchema();
            Entity entidade = parede.GetEntity(schema);
            if (entidade == null || !entidade.IsValid()) return pontos;

            List<double> pontosCm = LerListaCm(entidade.Get<string>(CampoPontos));
            List<double> comprimentoGravadoCm = LerListaCm(entidade.Get<string>(CampoComprimento));

            double comprimentoAtualCm = RevitVersionAdapter.InternoParaCentimetros(ComprimentoDaParede(parede));
            if (comprimentoGravadoCm.Count == 0
                || Math.Abs(comprimentoGravadoCm[0] - comprimentoAtualCm) > ToleranciaComprimentoCm)
            {
                desatualizada = true;
                return pontos;
            }

            pontos.AddRange(pontosCm
                .Select(RevitVersionAdapter.CentimetrosParaInterno)
                .OrderBy(p => p));
            return pontos;
        }

        /// <summary>
        /// Monta a lista de paineis de uma parede a partir dos pontos de corte. Sem pontos,
        /// devolve um unico painel com a parede inteira.
        /// </summary>
        public static List<PainelInfo> MontarPaineis(IList<double> pontosCorteInterno, double comprimentoInterno)
        {
            var limites = new List<double> { 0.0 };
            limites.AddRange(pontosCorteInterno
                .Where(p => p > 0.0 && p < comprimentoInterno)
                .OrderBy(p => p));
            limites.Add(comprimentoInterno);

            var paineis = new List<PainelInfo>();
            for (int i = 0; i < limites.Count - 1; i++)
            {
                paineis.Add(new PainelInfo
                {
                    Numero = i + 1,
                    InicioInterno = limites[i],
                    FimInterno = limites[i + 1]
                });
            }

            return paineis;
        }

        /// <summary>
        /// Paineis atuais de uma parede (lendo o que esta gravado nela).
        /// </summary>
        public static List<PainelInfo> ObterPaineis(Wall parede, out bool desatualizada)
        {
            List<double> pontos = LerPontosCorte(parede, out desatualizada);
            return MontarPaineis(pontos, ComprimentoDaParede(parede));
        }
    }
}