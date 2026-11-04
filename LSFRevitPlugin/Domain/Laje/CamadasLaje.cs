using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Uma camada vertical da laje, com cotas Z absolutas em mm (topo acima da base).
    /// </summary>
    public sealed class CamadaLaje
    {
        public const string NomeArgamassa = "Argamassa";
        public const string NomeTelha = "Telha";
        public const string NomePisoSeco = "PisoSeco";
        public const string NomeOSB = "OSB";
        public const string NomeCimenticia = "Cimenticia";
        public const string NomeViga = "Viga";

        /// <summary>Camadas que correspondem a placas planas geraveis (piso seco/OSB/cimenticia).</summary>
        public static bool EhPlaca(string nome)
        {
            return nome == NomePisoSeco || nome == NomeOSB || nome == NomeCimenticia;
        }

        public CamadaLaje(string nome, double topoMm, double baseMm)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Nome da camada e obrigatorio.", nameof(nome));
            if (double.IsNaN(topoMm) || double.IsInfinity(topoMm) || double.IsNaN(baseMm) || double.IsInfinity(baseMm))
                throw new ArgumentException("Cotas da camada devem ser numeros finitos.");
            if (baseMm >= topoMm)
                throw new ArgumentException("Base da camada deve ficar abaixo do topo.");

            Nome = nome;
            TopoMm = topoMm;
            BaseMm = baseMm;
        }

        public string Nome { get; }

        public double TopoMm { get; }

        public double BaseMm { get; }

        public double EspessuraMm => TopoMm - BaseMm;
    }

    /// <summary>
    /// Cotas das camadas da laje, de cima para baixo, a partir da cota do topo do conjunto.
    ///  - Laje umida: Argamassa (espessura ACIMA da onda), Telha (altura da onda, incluindo o
    ///    enchimento entre as ondas), Viga (altura da alma). Base da telha = topo da viga.
    ///  - Laje seca: PisoSeco, Viga.
    /// Espessura nominal da chapa da telha nao entra: a altura da onda ja e a do perfil.
    /// </summary>
    public sealed class CamadasLaje
    {
        private CamadasLaje(TipoLaje tipo, List<CamadaLaje> camadas)
        {
            Tipo = tipo;
            Camadas = camadas.AsReadOnly();
            TopoConjuntoMm = camadas[0].TopoMm;

            CamadaLaje viga = camadas[camadas.Count - 1];
            TopoVigaMm = viga.TopoMm;
            BaseVigaMm = viga.BaseMm;
        }

        public TipoLaje Tipo { get; }

        /// <summary>Camadas de cima para baixo; a ultima e sempre a viga.</summary>
        public IReadOnlyList<CamadaLaje> Camadas { get; }

        public double TopoConjuntoMm { get; }

        public double TopoVigaMm { get; }

        public double BaseVigaMm { get; }

        public double AlturaTotalMm => TopoConjuntoMm - BaseVigaMm;

        /// <summary>Camada pelo nome (ver constantes de CamadaLaje); nulo se a laje nao a tem.</summary>
        public CamadaLaje Camada(string nome)
        {
            foreach (CamadaLaje c in Camadas)
            {
                if (c.Nome == nome) return c;
            }

            return null;
        }

        public static CamadasLaje CalcularUmida(
            double cotaTopoMm, double alturaAlmaMm, double alturaOndaMm, double espessuraArgamassaAcimaMm)
        {
            ExigirFinito(cotaTopoMm, nameof(cotaTopoMm));
            ExigirPositivo(alturaAlmaMm, nameof(alturaAlmaMm));
            ExigirPositivo(alturaOndaMm, nameof(alturaOndaMm));
            ExigirPositivo(espessuraArgamassaAcimaMm, nameof(espessuraArgamassaAcimaMm));

            double topoOnda = cotaTopoMm - espessuraArgamassaAcimaMm;
            double baseTelha = topoOnda - alturaOndaMm;
            double baseViga = baseTelha - alturaAlmaMm;

            var camadas = new List<CamadaLaje>
            {
                new CamadaLaje(CamadaLaje.NomeArgamassa, cotaTopoMm, topoOnda),
                new CamadaLaje(CamadaLaje.NomeTelha, topoOnda, baseTelha),
                new CamadaLaje(CamadaLaje.NomeViga, baseTelha, baseViga)
            };

            return new CamadasLaje(TipoLaje.Umida, camadas);
        }

        /// <summary>
        /// Laje seca com composicao de placas escolhida pelo usuario: cada placa vira uma camada
        /// (de cima para baixo, na ordem da composicao) e a viga fica por ultimo.
        /// </summary>
        public static CamadasLaje CalcularSecaComPlacas(
            double cotaTopoMm, double alturaAlmaMm, ComposicaoCobertura composicao)
        {
            ExigirFinito(cotaTopoMm, nameof(cotaTopoMm));
            ExigirPositivo(alturaAlmaMm, nameof(alturaAlmaMm));
            if (composicao == null || composicao.Camadas.Count == 0)
                throw new ArgumentException("Composicao de placas nao informada.", nameof(composicao));

            var camadas = new List<CamadaLaje>();
            double topo = cotaTopoMm;

            foreach (CamadaCobertura camada in composicao.Camadas)
            {
                ExigirPositivo(camada.EspessuraMm, nameof(camada.EspessuraMm));
                double baseCamada = topo - camada.EspessuraMm;
                camadas.Add(new CamadaLaje(camada.Nome, topo, baseCamada));
                topo = baseCamada;
            }

            camadas.Add(new CamadaLaje(CamadaLaje.NomeViga, topo, topo - alturaAlmaMm));
            return new CamadasLaje(TipoLaje.Seca, camadas);
        }

        public static CamadasLaje CalcularSeca(double cotaTopoMm, double alturaAlmaMm, double espessuraPisoMm)
        {
            ExigirFinito(cotaTopoMm, nameof(cotaTopoMm));
            ExigirPositivo(alturaAlmaMm, nameof(alturaAlmaMm));
            ExigirPositivo(espessuraPisoMm, nameof(espessuraPisoMm));

            double topoViga = cotaTopoMm - espessuraPisoMm;
            double baseViga = topoViga - alturaAlmaMm;

            var camadas = new List<CamadaLaje>
            {
                new CamadaLaje(CamadaLaje.NomePisoSeco, cotaTopoMm, topoViga),
                new CamadaLaje(CamadaLaje.NomeViga, topoViga, baseViga)
            };

            return new CamadasLaje(TipoLaje.Seca, camadas);
        }

        /// <summary>
        /// Calcula pelas escolhas do usuario e pelo catalogo: cota e perfil vem de ParametrosLaje; altura
        /// da onda e argamassa (umida) ou camadas x espessura do piso (seca) vem do catalogo.
        /// A altura da alma e a LarguraAlmaMm do perfil (alma vertical na viga de piso).
        /// </summary>
        public static CamadasLaje Calcular(
            ParametrosLaje parametros,
            CatalogoLaje catalogo,
            double? alturaVigaOverrideMm = null)
        {
            if (parametros == null) throw new ArgumentNullException(nameof(parametros));
            if (catalogo == null) throw new ArgumentNullException(nameof(catalogo));
            if (parametros.Perfil == null) throw new ArgumentException("Perfil nao informado.", nameof(parametros));

            double alma = alturaVigaOverrideMm ?? parametros.AlturaVigaEfetivaMm();
            ExigirPositivo(alma, nameof(alturaVigaOverrideMm));

            if (parametros.Tipo == TipoLaje.Umida)
            {
                return CalcularUmida(
                    parametros.CotaMm,
                    alma,
                    catalogo.Telha.AlturaOndaMm.Valor,
                    catalogo.Argamassa.EspessuraAcimaOndaMm.Valor);
            }

            if (parametros.ComposicaoPlacas != ComposicaoTipo.Nenhuma)
            {
                return CalcularSecaComPlacas(parametros.CotaMm, alma, parametros.Composicao());
            }

            double espessuraPiso = catalogo.PisoSeco.NumeroCamadas.Valor * catalogo.PisoSeco.EspessuraMm.Valor;
            return CalcularSeca(parametros.CotaMm, alma, espessuraPiso);
        }

        private static void ExigirFinito(double valor, string nome)
        {
            if (double.IsNaN(valor) || double.IsInfinity(valor))
                throw new ArgumentOutOfRangeException(nome, "Valor deve ser um numero finito.");
        }

        private static void ExigirPositivo(double valor, string nome)
        {
            if (double.IsNaN(valor) || double.IsInfinity(valor) || valor <= 0.0)
                throw new ArgumentOutOfRangeException(nome, "Valor deve ser maior que zero.");
        }
    }
}
