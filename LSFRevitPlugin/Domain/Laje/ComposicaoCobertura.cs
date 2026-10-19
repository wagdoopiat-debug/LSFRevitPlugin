using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    public enum ComposicaoTipo
    {
        Nenhuma,
        OSB,
        Cimenticia,
        OSBMaisCimenticia
    }

    public sealed class CamadaCobertura
    {
        public CamadaCobertura(string nome, double espessuraMm)
        {
            Nome = nome;
            EspessuraMm = espessuraMm;
        }

        public string Nome { get; }
        public double EspessuraMm { get; }
    }

    public sealed class ComposicaoCobertura
    {
        public static readonly double[] EspessurasOSBMm = { 15.0, 16.0, 22.0, 25.0 };
        public static readonly double[] EspessurasCimenticiaMm = { 10.0, 20.0 };

        private ComposicaoCobertura(ComposicaoTipo tipo, List<CamadaCobertura> camadas)
        {
            Tipo = tipo;
            Camadas = camadas.AsReadOnly();
        }

        public ComposicaoTipo Tipo { get; }
        public IReadOnlyList<CamadaCobertura> Camadas { get; }

        public static ComposicaoCobertura CriarNenhuma()
        {
            return new ComposicaoCobertura(ComposicaoTipo.Nenhuma, new List<CamadaCobertura>());
        }

        public static ComposicaoCobertura CriarOSB(double espessuraMm)
        {
            var camadas = new List<CamadaCobertura>
            {
                new CamadaCobertura("OSB", espessuraMm)
            };
            return new ComposicaoCobertura(ComposicaoTipo.OSB, camadas);
        }

        public static ComposicaoCobertura CriarCimenticia(double espessuraMm)
        {
            var camadas = new List<CamadaCobertura>
            {
                new CamadaCobertura("Cimenticia", espessuraMm)
            };
            return new ComposicaoCobertura(ComposicaoTipo.Cimenticia, camadas);
        }

        public static ComposicaoCobertura CriarOSBMaisCimenticia(double espessuraOSBMm, double espessuraCimenticiaMm)
        {
            var camadas = new List<CamadaCobertura>
            {
                new CamadaCobertura("Cimenticia", espessuraCimenticiaMm),
                new CamadaCobertura("OSB", espessuraOSBMm)
            };
            return new ComposicaoCobertura(ComposicaoTipo.OSBMaisCimenticia, camadas);
        }

        /// <summary>Monta a composicao a partir do tipo e das espessuras escolhidas.</summary>
        public static ComposicaoCobertura Criar(ComposicaoTipo tipo, double espessuraOSBMm, double espessuraCimenticiaMm)
        {
            switch (tipo)
            {
                case ComposicaoTipo.OSB:
                    return CriarOSB(espessuraOSBMm);
                case ComposicaoTipo.Cimenticia:
                    return CriarCimenticia(espessuraCimenticiaMm);
                case ComposicaoTipo.OSBMaisCimenticia:
                    return CriarOSBMaisCimenticia(espessuraOSBMm, espessuraCimenticiaMm);
                default:
                    return CriarNenhuma();
            }
        }

        public double AlturaTotalMm
        {
            get
            {
                double total = 0.0;
                foreach (var c in Camadas) total += c.EspessuraMm;
                return total;
            }
        }

        public static bool UsaOSB(ComposicaoTipo tipo)
        {
            return tipo == ComposicaoTipo.OSB || tipo == ComposicaoTipo.OSBMaisCimenticia;
        }

        public static bool UsaCimenticia(ComposicaoTipo tipo)
        {
            return tipo == ComposicaoTipo.Cimenticia || tipo == ComposicaoTipo.OSBMaisCimenticia;
        }

        public static bool EspessuraOSBValida(double espessuraMm)
        {
            foreach (double e in EspessurasOSBMm) { if (System.Math.Abs(e - espessuraMm) < 1e-6) return true; }
            return false;
        }

        public static bool EspessuraCimenticiaValida(double espessuraMm)
        {
            foreach (double e in EspessurasCimenticiaMm) { if (System.Math.Abs(e - espessuraMm) < 1e-6) return true; }
            return false;
        }
    }
}
