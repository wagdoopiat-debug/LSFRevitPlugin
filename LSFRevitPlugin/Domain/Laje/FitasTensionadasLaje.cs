using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Uma fita tensionada de travamento: corre transversalmente (ao longo de v), na posicao u
    /// indicada, ligando as trelicas. Coordenadas do OrientacaoVigas, em mm.
    /// </summary>
    public sealed class FitaTensionada
    {
        public FitaTensionada(double uMm, double vInicioMm, double vFimMm)
        {
            if (vFimMm < vInicioMm)
                throw new ArgumentException("Fim da fita nao pode ser menor que o inicio.", nameof(vFimMm));

            UMm = uMm;
            VInicioMm = vInicioMm;
            VFimMm = vFimMm;
        }

        public double UMm { get; }

        public double VInicioMm { get; }

        public double VFimMm { get; }

        public double ComprimentoMm => VFimMm - VInicioMm;
    }

    /// <summary>
    /// Regra pura (GEOMETRICA DE PARTIDA, NAO NORMATIVA) das fitas tensionadas de travamento. A
    /// quantidade e o posicionamento de fitas nao estao definidos por norma nem por regra de
    /// projeto no repositorio: adota-se um passo alvo ao longo do vao, com um aviso registrado em
    /// conhecimento. O dimensionamento estrutural das fitas (numero de fios, pre-tensao, ligacoes)
    /// NAO faz parte desta classe e permanece pendente.
    /// </summary>
    public static class FitasTensionadasLaje
    {
        /// <summary>
        /// Passo alvo (mm) entre linhas de fita ao longo do vao -- GEOMETRICO DE PARTIDA, NAO
        /// NORMATIVO. O projetista deve confirmar/ajustar.
        /// </summary>
        public const double PassoPadraoMm = 1500.0;

        /// <summary>Largura da fita ao longo do vao (mm) -- GEOMETRICA DE PARTIDA, NAO NORMATIVA.</summary>
        public const double LarguraMm = 30.0;

        /// <summary>Espessura da fita (mm) -- GEOMETRICA DE PARTIDA, NAO NORMATIVA.</summary>
        public const double EspessuraMm = 1.0;

        /// <summary>
        /// Cota (mm) da BASE da fita quando esta fica na FACE INFERIOR da estrutura de vigamento:
        /// face inferior do banzo inferior da trelica ou face inferior da viga convencional (perfil
        /// simples, composta ou caixao). A fita ocupa o intervalo [base, base + EspessuraMm], com
        /// o TOPO coincidindo com a face inferior da estrutura -- logo nao fica embutida na secao,
        /// nao atravessa o perfil e nao e deslocada para o topo.
        /// </summary>
        public static double BaseNaFaceInferiorMm(double faceInferiorEstruturaMm)
        {
            return faceInferiorEstruturaMm - EspessuraMm;
        }

        /// <summary>
        /// Calcula a face inferior externa do banzo inferior da trelica.
        /// A dimensao vertical da secao depende da montagem:
        /// Encaixada: AbaMm; FaceComFace: LarguraAlmaMm.
        /// AlturaTrelicaMm e a distancia entre os eixos dos banzos.
        /// </summary>
        public static double FaceInferiorBanzoInferiorMm(
            double zCentroVigaMm,
            double alturaTrelicaMm,
            MontagemTrelica montagem,
            double abaMm,
            double larguraAlmaMm)
        {
            ExigirPositivoFinito(alturaTrelicaMm, nameof(alturaTrelicaMm));
            ExigirPositivoFinito(abaMm, nameof(abaMm));
            ExigirPositivoFinito(larguraAlmaMm, nameof(larguraAlmaMm));

            if (double.IsNaN(zCentroVigaMm) || double.IsInfinity(zCentroVigaMm))
                throw new ArgumentOutOfRangeException(nameof(zCentroVigaMm));

            double meiaDimensaoVertical;

            switch (montagem)
            {
                case MontagemTrelica.Encaixada:
                    meiaDimensaoVertical = abaMm / 2.0;
                    break;

                case MontagemTrelica.FaceComFace:
                    meiaDimensaoVertical = larguraAlmaMm / 2.0;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(montagem));
            }

            return zCentroVigaMm - alturaTrelicaMm / 2.0 - meiaDimensaoVertical;
        }

        private static void ExigirPositivoFinito(double valor, string nome)
        {
            if (double.IsNaN(valor) || double.IsInfinity(valor) || valor <= 0.0)
                throw new ArgumentOutOfRangeException(nome, "Valor deve ser positivo e finito.");
        }

        /// <summary>Tolerancia (mm) para nao gerar fita sobre as bordas nem fora do vao.</summary>
        public const double ToleranciaMm = 1.0;

        /// <summary>Minimo de vigas para que exista travamento "entre trelicas".</summary>
        public const int MinimoVigas = 2;

        /// <summary>
        /// Linhas de fita ao longo de u: uma em cada passo alvo, estritamente dentro do vao
        /// (sem sobrepor as bordas, onde fica o travamento de apoio). Cada fita cobre toda a
        /// largura transversal do envolvente (vMin..vMax). Devolve lista vazia se degenerado,
        /// se o passo for invalido ou se nao houver ao menos <see cref="MinimoVigas"/> vigas.
        /// </summary>
        public static List<FitaTensionada> Calcular(
            OrientacaoVigas orientacao, ResultadoLayoutVigas layout, double passoMm)
        {
            var fitas = new List<FitaTensionada>();
            if (orientacao == null || layout == null) return fitas;
            if (layout.Vigas == null || layout.Vigas.Count < MinimoVigas) return fitas;

            if (double.IsNaN(passoMm) || double.IsInfinity(passoMm) || passoMm <= 0.0)
                passoMm = PassoPadraoMm;
            if (double.IsNaN(passoMm) || double.IsInfinity(passoMm) || passoMm <= 0.0) return fitas;

            double uMin = orientacao.UMinMm;
            double uMax = orientacao.UMaxMm;
            double vMin = orientacao.VMinMm;
            double vMax = orientacao.VMaxMm;

            if (vMax - vMin <= ToleranciaMm) return fitas;

            for (int k = 1; ; k++)
            {
                double u = uMin + k * passoMm;
                if (u >= uMax - ToleranciaMm) break;

                fitas.Add(new FitaTensionada(u, vMin, vMax));
            }

            return fitas;
        }
    }
}
