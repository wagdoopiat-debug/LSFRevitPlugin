using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Uma placa (chapa) de cobertura ja posicionada, com contorno em coordenadas de mundo (mm)
    /// e a cota/espessura da camada. O contorno pode ser um recorte parcial da placa original
    /// (quando ela ultrapassa a borda da laje).
    /// </summary>
    public sealed class PlacaPaginada
    {
        public PlacaPaginada(
            IReadOnlyList<Ponto2DMm> contornoMundo, double baseMm, double espessuraMm,
            int fila, int coluna, int camada)
        {
            ContornoMundo = contornoMundo;
            BaseMm = baseMm;
            EspessuraMm = espessuraMm;
            Fila = fila;
            Coluna = coluna;
            Camada = camada;
        }

        public IReadOnlyList<Ponto2DMm> ContornoMundo { get; }

        public double BaseMm { get; }

        public double EspessuraMm { get; }

        public int Fila { get; }

        public int Coluna { get; }

        public int Camada { get; }
    }

    /// <summary>Resultado do parcelamento de uma camada de placas.</summary>
    public sealed class ResultadoPaginacao
    {
        public List<PlacaPaginada> Placas { get; } = new List<PlacaPaginada>();

        public List<string> Avisos { get; } = new List<string>();

        /// <summary>true quando a placa de fato foi dividida em varias chapas.</summary>
        public bool Paginou { get; internal set; }

        /// <summary>true quando foi preciso cair na placa unica (contorno nao convexo).</summary>
        public bool FallbackContornoUnico { get; internal set; }
    }

    /// <summary>
    /// Parcelamento das placas de cobertura em chapas comerciais, com juntas. As chapas sao
    /// posicionadas perpendicularmente as vigas: o lado menor (largura) corre ao longo da viga
    /// (u) e o lado maior (comprimento) corre transversalmente (v). Cada chapa e recortada pelo
    /// contorno real da laje (recorte de poligono). Contorno convexo usa um recorte direto;
    /// contorno nao convexo e triangulado e recortado por celula, admitindo mais de um pedaco por
    /// celula. Puro, sem Revit.
    ///
    /// ATENCAO: dimensoes comerciais e juntas sao de partida, nao normativas nem comerciais
    /// confirmadas -- cabe ao projetista confirmar a modulacao real.
    /// </summary>
    public static class PaginacaoPlacas
    {
        /// <summary>Dimensoes comerciais usuais de chapa de OSB (mm).</summary>
        public const double LarguraOSBMm = 1200.0;

        public const double ComprimentoOSBMm = 2400.0;

        /// <summary>Dimensoes comerciais usuais de placa cimenticia (mm).</summary>
        public const double LarguraCimenticiaMm = 1200.0;

        public const double ComprimentoCimenticiaMm = 2400.0;

        /// <summary>Folga padrao entre chapas (junta), em mm.</summary>
        public const double JuntaPadraoMm = 3.0;

        /// <summary>Limite de seguranca do numero de chapas por camada, para nao explodir o modelo.</summary>
        public const int MaxPlacas = 3000;

        /// <summary>Area minima (mm2) de um recorte para ser considerado uma chapa valida.</summary>
        public const double AreaMinimaRecorteMm2 = 1.0;

        /// <summary>
        /// Dimensoes da chapa conforme a camada, para que o lado maior do OSB corra PERPENDICULAR
        /// as vigas (transversal, eixo v) e o da placa cimenticia corra PARALELO as vigas
        /// (longitudinal, eixo u) -- orientacoes opostas, evitando juntas coincidentes.
        /// <paramref name="aoLongoUMm"/> e o lado ao longo da viga (u); <paramref name="transversalVMm"/>
        /// e o lado transversal (v).
        /// </summary>
        public static void DimensoesOrientadas(bool osb, out double aoLongoUMm, out double transversalVMm)
        {
            if (osb)
            {
                // OSB: lado menor (largura) ao longo da viga; lado maior (comprimento) transversal.
                aoLongoUMm = LarguraOSBMm;
                transversalVMm = ComprimentoOSBMm;
            }
            else
            {
                // Cimenticia: rotacionada 90 graus -- lado maior (comprimento) ao longo da viga.
                aoLongoUMm = ComprimentoCimenticiaMm;
                transversalVMm = LarguraCimenticiaMm;
            }
        }

        /// <summary>true quando a camada e de OSB (ou piso seco do catalogo, tratado como OSB).</summary>
        public static bool EhCamadaOSB(string nomeCamada)
        {
            return nomeCamada == CamadaLaje.NomeOSB || nomeCamada == CamadaLaje.NomePisoSeco;
        }

        public static bool EhConvexo(ContornoLaje contorno)
        {
            if (contorno == null) return false;
            return EhConvexo(contorno.Pontos);
        }

        public static bool EhConvexo(IReadOnlyList<Ponto2DMm> pontos)
        {
            return RecortePoligono.EhConvexo(pontos);
        }

        /// <summary>
        /// Divide a camada em chapas. <paramref name="larguraMm"/> e o lado ao longo da viga (u),
        /// <paramref name="comprimentoMm"/> o lado transversal (v). <paramref name="offsetCamadaMm"/>
        /// desloca toda a malha (para defasar camadas empilhadas); <paramref name="offsetLinhaMm"/>
        /// desloca linhas alternadas (juntas desencontradas).
        /// </summary>
        public static ResultadoPaginacao Calcular(
            ContornoLaje contorno,
            OrientacaoVigas orientacao,
            double larguraMm,
            double comprimentoMm,
            double baseMm,
            double espessuraMm,
            double juntaMm,
            double offsetCamadaMm,
            double offsetLinhaMm,
            int camada = 0,
            double margemMm = 0.0)
        {
            var resultado = new ResultadoPaginacao();

            if (contorno == null || !contorno.EhValido || orientacao == null)
            {
                resultado.Avisos.Add("Parcelamento de placas ignorado: contorno ou orientacao invalidos.");
                AdicionarPlacaUnica(resultado, contorno, baseMm, espessuraMm);
                return resultado;
            }

            if (larguraMm <= 0.0 || comprimentoMm <= 0.0)
            {
                resultado.Avisos.Add("Parcelamento de placas ignorado: dimensoes de chapa invalidas.");
                AdicionarPlacaUnica(resultado, contorno, baseMm, espessuraMm);
                return resultado;
            }

            if (!EhConvexo(contorno))
            {
                resultado.Avisos.Add(
                    "Contorno nao convexo: chapas recortadas por triangulacao do contorno. "
                    + "Algumas chapas podem sair em dois pedacos; confirme a modulacao real.");
            }

            if (double.IsNaN(juntaMm) || double.IsInfinity(juntaMm) || juntaMm < 0.0)
                juntaMm = JuntaPadraoMm;
            if (double.IsNaN(margemMm) || double.IsInfinity(margemMm) || margemMm < 0.0)
                margemMm = 0.0;
            if (double.IsNaN(offsetCamadaMm) || double.IsInfinity(offsetCamadaMm)) offsetCamadaMm = 0.0;
            if (double.IsNaN(offsetLinhaMm) || double.IsInfinity(offsetLinhaMm)) offsetLinhaMm = 0.0;

            // Contorno no plano (u, v).
            var janela = new List<Ponto2DMm>(contorno.Pontos.Count);
            double uMin = double.MaxValue;
            double uMax = double.MinValue;
            double vMin = double.MaxValue;
            double vMax = double.MinValue;

            foreach (Ponto2DMm p in contorno.Pontos)
            {
                double u = orientacao.CoordenadaU(p);
                double v = orientacao.CoordenadaV(p);
                janela.Add(new Ponto2DMm(u, v));
                if (u < uMin) uMin = u;
                if (u > uMax) uMax = u;
                if (v < vMin) vMin = v;
                if (v > vMax) vMax = v;
            }

            if (margemMm > 0.0)
            {
                uMin += margemMm;
                uMax -= margemMm;
                vMin += margemMm;
                vMax -= margemMm;
            }

            if (uMax <= uMin || vMax <= vMin)
            {
                resultado.Avisos.Add("Parcelamento de placas ignorado: contorno pequeno demais para a margem.");
                AdicionarPlacaUnica(resultado, contorno, baseMm, espessuraMm);
                return resultado;
            }

            double passoU = larguraMm + juntaMm;
            double passoV = comprimentoMm + juntaMm;

            List<List<Ponto2DMm>> convexos = RecortePoligono.TriangulosConvexos(janela);

            int fila = 0;
            bool atingiuLimite = false;

            for (double v = vMin - offsetCamadaMm; v < vMax && !atingiuLimite; v += passoV, fila++)
            {
                double uOffset = offsetCamadaMm + ((fila % 2 == 0) ? 0.0 : offsetLinhaMm);

                int coluna = 0;
                for (double u = uMin - uOffset; u < uMax; u += passoU, coluna++)
                {
                    var celula = new List<Ponto2DMm>
                    {
                        new Ponto2DMm(u, v),
                        new Ponto2DMm(u + larguraMm, v),
                        new Ponto2DMm(u + larguraMm, v + comprimentoMm),
                        new Ponto2DMm(u, v + comprimentoMm)
                    };

                    List<List<Ponto2DMm>> pedacos = RecortePoligono.RecortarConvexosPorCelula(
                        convexos, celula, AreaMinimaRecorteMm2);

                    foreach (List<Ponto2DMm> recorte in pedacos)
                    {
                        var mundo = new List<Ponto2DMm>(recorte.Count);
                        foreach (Ponto2DMm p in recorte)
                        {
                            mundo.Add(orientacao.ParaMundo(p.XMm, p.YMm));
                        }

                        resultado.Placas.Add(new PlacaPaginada(mundo, baseMm, espessuraMm, fila, coluna, camada));

                        if (resultado.Placas.Count >= MaxPlacas)
                        {
                            atingiuLimite = true;
                            break;
                        }
                    }

                    if (atingiuLimite) break;
                }
            }

            if (resultado.Placas.Count == 0)
            {
                resultado.Avisos.Add("Parcelamento nao gerou chapas; usando placa unica.");
                AdicionarPlacaUnica(resultado, contorno, baseMm, espessuraMm);
                return resultado;
            }

            resultado.Paginou = true;
            if (atingiuLimite)
            {
                resultado.Avisos.Add(
                    "Parcelamento das placas atingiu o limite de seguranca (" + MaxPlacas + " chapas).");
            }

            return resultado;
        }

        private static void AdicionarPlacaUnica(
            ResultadoPaginacao resultado, ContornoLaje contorno, double baseMm, double espessuraMm)
        {
            if (contorno == null || !contorno.EhValido) return;

            resultado.Placas.Add(new PlacaPaginada(
                new List<Ponto2DMm>(contorno.Pontos), baseMm, espessuraMm, 0, 0, 0));
        }
    }
}
