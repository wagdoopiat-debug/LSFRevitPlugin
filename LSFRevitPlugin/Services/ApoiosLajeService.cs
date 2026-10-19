using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LSFRevitPlugin.Core;
using LSFRevitPlugin.Domain.Laje;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Services
{
    /// <summary>
    /// Descobre os apoios (montantes de parede) sob/ao redor do contorno da laje e projeta as suas
    /// posicoes no eixo transversal (v) da orientacao das vigas, para alinhar as vigas INTERNAS
    /// aos montantes correspondentes. As vigas de borda nao sao movidas (regra preservada).
    ///
    /// Reutiliza a leitura de estrutura ja existente (<see cref="LeituraEstruturaParedeService"/>):
    /// nao cria novas regras de identidade. Se nao houver parede/montante, devolve lista vazia e
    /// aviso claro -- nunca desloca vigas silenciosamente.
    /// </summary>
    public static class ApoiosLajeService
    {
        /// <summary>Folga (mm) em torno do contorno para localizar paredes de apoio.</summary>
        public const double MargemBuscaMm = 100.0;

        /// <summary>Tolerancia (mm) para considerar duas posicoes de montante coincidentes.</summary>
        public const double ToleranciaDedupeMm = 1.0;

        /// <summary>
        /// Posicoes v (mm) dos montantes de parede projetados na orientacao das vigas, ordenadas.
        /// Devolve lista vazia (com o aviso correspondente) quando nao ha parede/montante.
        /// </summary>
        public static List<double> PosicoesApoioMm(
            Document doc,
            ContornoLaje contorno,
            OrientacaoVigas orientacao,
            out List<string> avisos)
        {
            avisos = new List<string>();
            var posicoes = new List<double>();
            if (doc == null || contorno == null || orientacao == null) return posicoes;

            List<Wall> paredes = ParedesSobreContorno(doc, contorno);
            if (paredes.Count == 0)
            {
                avisos.Add(
                    "Alinhamento viga-montante: nenhuma parede de apoio encontrada sob/ao redor do " +
                    "contorno; as vigas internas permanecem na grade de espacamento.");
                return posicoes;
            }

            double vMin = orientacao.VMinMm - ToleranciaDedupeMm;
            double vMax = orientacao.VMaxMm + ToleranciaDedupeMm;

            foreach (Wall parede in paredes)
            {
                LeituraParede leitura;
                try
                {
                    leitura = LeituraEstruturaParedeService.Ler(doc, parede);
                }
                catch
                {
                    continue;
                }

                if (leitura == null || leitura.MontantesMm == null) continue;

                foreach (double montanteMm in leitura.MontantesMm)
                {
                    XYZ ponto = leitura.Inicio
                        + leitura.Direcao.Multiply(RevitVersionAdapter.MilimetrosParaInterno(montanteMm));
                    double xMm = RevitVersionAdapter.InternoParaMilimetros(ponto.X);
                    double yMm = RevitVersionAdapter.InternoParaMilimetros(ponto.Y);
                    double vMm = xMm * orientacao.VX + yMm * orientacao.VY;

                    if (vMm < vMin || vMm > vMax) continue;
                    AdicionarUnico(posicoes, vMm);
                }
            }

            if (posicoes.Count == 0)
            {
                avisos.Add(
                    "Alinhamento viga-montante: paredes encontradas, mas nenhum montante projetado " +
                    "dentro do contorno; as vigas internas permanecem na grade de espacamento.");
                return posicoes;
            }

            posicoes.Sort();
            return posicoes;
        }

        private static void AdicionarUnico(List<double> posicoes, double vMm)
        {
            foreach (double existente in posicoes)
            {
                if (Math.Abs(existente - vMm) <= ToleranciaDedupeMm) return;
            }

            posicoes.Add(vMm);
        }

        private static List<Wall> ParedesSobreContorno(Document doc, ContornoLaje contorno)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (Ponto2DMm p in contorno.Pontos)
            {
                minX = Math.Min(minX, p.XMm);
                minY = Math.Min(minY, p.YMm);
                maxX = Math.Max(maxX, p.XMm);
                maxY = Math.Max(maxY, p.YMm);
            }

            var paredes = new List<Wall>();
            foreach (Wall parede in new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType()
                .Cast<Wall>())
            {
                BoundingBoxXYZ caixa = parede.get_BoundingBox(null);
                if (caixa == null) continue;

                double wMinX = RevitVersionAdapter.InternoParaMilimetros(caixa.Min.X) - MargemBuscaMm;
                double wMinY = RevitVersionAdapter.InternoParaMilimetros(caixa.Min.Y) - MargemBuscaMm;
                double wMaxX = RevitVersionAdapter.InternoParaMilimetros(caixa.Max.X) + MargemBuscaMm;
                double wMaxY = RevitVersionAdapter.InternoParaMilimetros(caixa.Max.Y) + MargemBuscaMm;

                bool sobrepoe = wMinX <= maxX && wMaxX >= minX && wMinY <= maxY && wMaxY >= minY;
                if (sobrepoe) paredes.Add(parede);
            }

            return paredes;
        }
    }
}
