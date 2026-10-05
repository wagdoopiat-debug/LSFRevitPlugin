using System;
using System.Collections.Generic;
using System.Linq;

namespace LSFRevitPlugin.Domain.Boards
{
    /// <summary>
    /// Prepara a entrada do Paginador para UM painel de UMA face, a partir de dados medidos na
    /// parede inteira (posicoes ao longo da parede, em mm). Trabalha so com medidas (mm), sem
    /// depender do Revit.
    /// </summary>
    public static class PreparoEntradaPaginacao
    {
        private const double Eps = 0.5;
        private const double ToleranciaMontanteDuplicado = 1.0;

        // Altura do corte de uma interrupcao: bem maior que qualquer parede, para o corte ir de
        // ponta a ponta sem mexer nas regras de distancia das aberturas.
        private const double AlturaDaInterrupcaoMm = 100000.0;

        /// <param name="numeroPainel">Numero do painel (usado nos codigos das placas).</param>
        /// <param name="numeroFace">1 ou 2. A face 2 usa o desencontro oposto ao da face 1.</param>
        /// <param name="inicioPainelMm">Inicio das placas ao longo da parede (a partir da ponta inicial).</param>
        /// <param name="fimPainelMm">Fim das placas ao longo da parede.</param>
        /// <param name="alturaMm">Altura da parede.</param>
        /// <param name="montantesNaParedeMm">Posicao (eixo) dos montantes onde uma junta pode cair, ao longo da parede.</param>
        /// <param name="aberturasNaParedeMm">Aberturas em coordenadas da parede (U ao longo da parede, Z a partir do piso).</param>
        /// <param name="interrupcoesNaParedeMm">
        /// Trechos da parede (U0 a U1) onde nao ha placa, de ponta a ponta na altura: onde outra
        /// parede cruza esta (so U0 e U1 contam). Opcional.
        /// </param>
        public static EntradaPaginacao Criar(
            int numeroPainel,
            int numeroFace,
            double inicioPainelMm,
            double fimPainelMm,
            double alturaMm,
            IEnumerable<double> montantesNaParedeMm,
            IEnumerable<RetanguloMm> aberturasNaParedeMm,
            TipoPlaca tipo,
            MedidaPlaca medida,
            OrientacaoPlaca orientacao,
            IEnumerable<RetanguloMm> interrupcoesNaParedeMm = null)
        {
            if (medida == null) throw new ArgumentNullException(nameof(medida));
            if (montantesNaParedeMm == null) throw new ArgumentNullException(nameof(montantesNaParedeMm));
            if (aberturasNaParedeMm == null) throw new ArgumentNullException(nameof(aberturasNaParedeMm));
            if (fimPainelMm - inicioPainelMm <= Eps)
                throw new ArgumentException("O fim do painel deve ser maior que o inicio.", nameof(fimPainelMm));
            if (alturaMm <= Eps)
                throw new ArgumentException("A altura deve ser maior que zero.", nameof(alturaMm));
            if (numeroFace < 1)
                throw new ArgumentException("O numero da face comeca em 1.", nameof(numeroFace));

            List<RetanguloMm> aberturas = AberturasNoPainel(aberturasNaParedeMm, inicioPainelMm, fimPainelMm, alturaMm);
            if (interrupcoesNaParedeMm != null)
            {
                aberturas.AddRange(InterrupcoesNoPainel(interrupcoesNaParedeMm, inicioPainelMm, fimPainelMm));
            }

            return new EntradaPaginacao
            {
                NumeroPainel = numeroPainel,
                NumeroFace = numeroFace,
                LarguraPainelMm = fimPainelMm - inicioPainelMm,
                AlturaMm = alturaMm,
                PosicoesMontantesMm = MontantesNoPainel(montantesNaParedeMm, inicioPainelMm, fimPainelMm),
                Aberturas = aberturas,
                Tipo = tipo,
                Medida = medida,
                Orientacao = orientacao,
                IndiceDeDesencontro = (numeroFace - 1) % 2
            };
        }

        /// <summary>
        /// Posicoes dos montantes que caem dentro do painel, em coordenadas do painel, ordenadas e
        /// sem repetidas (montantes a menos de 1 mm um do outro contam como um so).
        /// </summary>
        public static List<double> MontantesNoPainel(
            IEnumerable<double> montantesNaParedeMm, double inicioPainelMm, double fimPainelMm)
        {
            double largura = fimPainelMm - inicioPainelMm;
            var resultado = new List<double>();

            foreach (double posicao in montantesNaParedeMm.Select(m => m - inicioPainelMm).OrderBy(m => m))
            {
                if (posicao < -Eps || posicao > largura + Eps) continue;

                if (resultado.Count > 0 && posicao - resultado[resultado.Count - 1] < ToleranciaMontanteDuplicado) continue;

                resultado.Add(posicao);
            }

            return resultado;
        }

        /// <summary>
        /// Aberturas cortadas pelos limites do painel e do piso ao teto, em coordenadas do painel.
        /// Aberturas fora do painel (ou so encostadas nele) sao descartadas.
        /// </summary>
        public static List<RetanguloMm> AberturasNoPainel(
            IEnumerable<RetanguloMm> aberturasNaParedeMm, double inicioPainelMm, double fimPainelMm, double alturaMm)
        {
            double largura = fimPainelMm - inicioPainelMm;
            var resultado = new List<RetanguloMm>();

            foreach (RetanguloMm a in aberturasNaParedeMm)
            {
                var recortada = new RetanguloMm(
                    Math.Max(a.U0 - inicioPainelMm, 0.0),
                    Math.Min(a.U1 - inicioPainelMm, largura),
                    Math.Max(a.Z0, 0.0),
                    Math.Min(a.Z1, alturaMm));

                if (recortada.TemArea) resultado.Add(recortada);
            }

            return resultado;
        }

        /// <summary>
        /// Interrupcoes (onde outra parede cruza) cortadas pelos limites do painel, em coordenadas
        /// do painel. So U0 e U1 importam: o corte vai de ponta a ponta na altura.
        /// </summary>
        public static List<RetanguloMm> InterrupcoesNoPainel(
            IEnumerable<RetanguloMm> interrupcoesNaParedeMm, double inicioPainelMm, double fimPainelMm)
        {
            double largura = fimPainelMm - inicioPainelMm;
            var resultado = new List<RetanguloMm>();

            foreach (RetanguloMm interrupcao in interrupcoesNaParedeMm)
            {
                double u0 = Math.Max(interrupcao.U0 - inicioPainelMm, 0.0);
                double u1 = Math.Min(interrupcao.U1 - inicioPainelMm, largura);

                if (u1 - u0 > Eps)
                {
                    resultado.Add(new RetanguloMm(u0, u1, -AlturaDaInterrupcaoMm, AlturaDaInterrupcaoMm));
                }
            }

            return resultado;
        }
    }
}