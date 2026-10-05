using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LSFRevitPlugin.Domain.Boards
{
    /// <summary>Tipos de placa de fechamento.</summary>
    public enum TipoPlaca
    {
        Cimenticia,
        GlasrocX,
        Osb,
        GessoRu,
        GessoSt,
        GessoRf,
        GessoPerforma,
        PerformaRu
    }

    /// <summary>Como a placa e instalada na parede.</summary>
    public enum OrientacaoPlaca
    {
        /// <summary>Lado longo da placa na vertical.</summary>
        EmPe,

        /// <summary>Lado longo da placa na horizontal (fiadas).</summary>
        Deitada
    }

    /// <summary>
    /// Medida de uma placa. Os lados sao guardados sem orientacao (curto/longo): se a placa fica
    /// em pe ou deitada e decidido na paginacao.
    /// </summary>
    public sealed class MedidaPlaca : IEquatable<MedidaPlaca>
    {
        private const double ToleranciaMm = 0.5;

        public double LadoCurtoMm { get; }
        public double LadoLongoMm { get; }

        /// <summary>true se foi cadastrada pelo usuario; false se e uma medida de fabrica.</summary>
        public bool Personalizada { get; }

        public MedidaPlaca(double ladoCurtoMm, double ladoLongoMm, bool personalizada = false)
        {
            LadoCurtoMm = Math.Min(ladoCurtoMm, ladoLongoMm);
            LadoLongoMm = Math.Max(ladoCurtoMm, ladoLongoMm);
            Personalizada = personalizada;
        }

        /// <summary>Texto para listas, por exemplo "1,20 x 2,40 m".</summary>
        public string Descricao
        {
            get
            {
                CultureInfo pt = CultureInfo.GetCultureInfo("pt-BR");
                return string.Format(pt, "{0:0.00} x {1:0.00} m", LadoCurtoMm / 1000.0, LadoLongoMm / 1000.0);
            }
        }

        public bool Equals(MedidaPlaca outra)
        {
            if (outra == null) return false;
            return Math.Abs(LadoCurtoMm - outra.LadoCurtoMm) < ToleranciaMm
                && Math.Abs(LadoLongoMm - outra.LadoLongoMm) < ToleranciaMm;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MedidaPlaca);
        }

        public override int GetHashCode()
        {
            return (int)Math.Round(LadoCurtoMm) * 31 + (int)Math.Round(LadoLongoMm);
        }
    }

    /// <summary>
    /// Regras de fixacao e instalacao de um tipo de placa. Sao valores de partida: o que esta
    /// marcado como PrecisaValidar ainda nao tem fonte direta e deve ser conferido com o manual
    /// do fabricante antes de ser usado em obra.
    /// </summary>
    public sealed class RegraFixacaoPlaca
    {
        /// <summary>Espessura de partida da placa, em mm.</summary>
        public double EspessuraPadraoMm { get; set; }

        /// <summary>Espacamento maximo entre parafusos ao longo de cada montante/guia, em mm.</summary>
        public double EspacamentoAoLongoDoApoioMm { get; set; }

        /// <summary>Distancia do parafuso a borda da placa, em mm (minima ou de referencia).</summary>
        public double DistanciaDaBordaMm { get; set; }

        /// <summary>Folga entre placas vizinhas (junta), em mm. 0 = placas encostadas.</summary>
        public double JuntaEntrePlacasMm { get; set; }

        /// <summary>Folga entre a base da placa e o piso, em mm.</summary>
        public double FolgaDoPisoMm { get; set; }

        /// <summary>Parafuso de partida (descricao curta).</summary>
        public string ParafusoPadrao { get; set; }

        /// <summary>true = valores sem fonte direta, para conferir com o fabricante.</summary>
        public bool PrecisaValidar { get; set; }

        /// <summary>De onde vieram os valores (para o relatorio e para quem for conferir).</summary>
        public string Fonte { get; set; }
    }

    /// <summary>
    /// Catalogo de tipos de placa, medidas disponiveis (de fabrica + cadastradas pelo usuario) e
    /// regras de fixacao. As medidas cadastradas ficam num arquivo de texto por usuario.
    /// </summary>
    public sealed class PlacaCatalogo
    {
        /// <summary>Folga entre a base da placa e o piso, definida pelo projeto para todos os tipos, em mm.</summary>
        public const double FolgaDoPisoPadraoMm = 15.0;

        private static readonly Dictionary<TipoPlaca, RegraFixacaoPlaca> Regras = CriarRegras();
        private static PlacaCatalogo _padrao;

        private readonly string _caminhoArquivo;
        private readonly List<KeyValuePair<TipoPlaca, MedidaPlaca>> _personalizadas =
            new List<KeyValuePair<TipoPlaca, MedidaPlaca>>();

        /// <param name="caminhoArquivo">Arquivo onde as medidas cadastradas ficam salvas. Nulo = so em memoria.</param>
        public PlacaCatalogo(string caminhoArquivo = null)
        {
            _caminhoArquivo = caminhoArquivo;
            Carregar();
        }

        /// <summary>Catalogo do usuario atual (arquivo em %APPDATA%\LSFRevitPlugin).</summary>
        public static PlacaCatalogo Padrao
        {
            get
            {
                if (_padrao == null)
                {
                    string pasta = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "LSFRevitPlugin");
                    _padrao = new PlacaCatalogo(Path.Combine(pasta, "placas_personalizadas.txt"));
                }
                return _padrao;
            }
        }

        public static IList<TipoPlaca> TodosOsTipos()
        {
            return Enum.GetValues(typeof(TipoPlaca)).Cast<TipoPlaca>().ToList();
        }

        public static string NomeDoTipo(TipoPlaca tipo)
        {
            switch (tipo)
            {
                case TipoPlaca.Cimenticia: return "Placa cimentícia";
                case TipoPlaca.GlasrocX: return "Glasroc X";
                case TipoPlaca.Osb: return "OSB";
                case TipoPlaca.GessoRu: return "Gesso RU";
                case TipoPlaca.GessoSt: return "Gesso ST";
                case TipoPlaca.GessoRf: return "Gesso RF";
                case TipoPlaca.GessoPerforma: return "Gesso Performa";
                case TipoPlaca.PerformaRu: return "Performa RU";
                default: return tipo.ToString();
            }
        }

        public static RegraFixacaoPlaca RegraDoTipo(TipoPlaca tipo)
        {
            return Regras[tipo];
        }

        /// <summary>Medidas de fabrica de um tipo: Glasroc X, cimenticia e OSB so 1,20 x 2,40; gessos tambem 1,20 x 1,80.</summary>
        public static IList<MedidaPlaca> MedidasDeFabrica(TipoPlaca tipo)
        {
            switch (tipo)
            {
                case TipoPlaca.Cimenticia:
                case TipoPlaca.GlasrocX:
                case TipoPlaca.Osb:
                    return new List<MedidaPlaca> { new MedidaPlaca(1200, 2400) };

                default:
                    return new List<MedidaPlaca>
                    {
                        new MedidaPlaca(1200, 2400),
                        new MedidaPlaca(1200, 1800)
                    };
            }
        }

        /// <summary>Medidas de fabrica + as cadastradas pelo usuario para o tipo.</summary>
        public IList<MedidaPlaca> MedidasDisponiveis(TipoPlaca tipo)
        {
            List<MedidaPlaca> lista = MedidasDeFabrica(tipo).ToList();
            lista.AddRange(_personalizadas.Where(p => p.Key == tipo).Select(p => p.Value));
            return lista;
        }

        /// <summary>
        /// Cadastra uma nova medida para um tipo de placa. A ordem dos lados nao importa.
        /// Devolve false (e o motivo em erro) se a medida for invalida ou ja existir.
        /// </summary>
        public bool Cadastrar(TipoPlaca tipo, double lado1Mm, double lado2Mm, out string erro)
        {
            erro = null;

            double curto = Math.Min(lado1Mm, lado2Mm);
            double longo = Math.Max(lado1Mm, lado2Mm);

            if (curto < 100 || longo > 6000)
            {
                erro = "Medida fora do aceitavel: cada lado deve ter ao menos 100 mm e o maior lado no maximo 6000 mm.";
                return false;
            }

            var nova = new MedidaPlaca(curto, longo, true);
            if (MedidasDisponiveis(tipo).Any(m => m.Equals(nova)))
            {
                erro = "Essa medida ja existe para este tipo de placa.";
                return false;
            }

            _personalizadas.Add(new KeyValuePair<TipoPlaca, MedidaPlaca>(tipo, nova));
            Salvar();
            return true;
        }

        /// <summary>Remove uma medida cadastrada pelo usuario. As de fabrica nao podem ser removidas.</summary>
        public bool Remover(TipoPlaca tipo, MedidaPlaca medida)
        {
            int removidas = _personalizadas.RemoveAll(p => p.Key == tipo && p.Value.Equals(medida));
            if (removidas == 0) return false;

            Salvar();
            return true;
        }

        private void Carregar()
        {
            if (string.IsNullOrEmpty(_caminhoArquivo) || !File.Exists(_caminhoArquivo)) return;

            try
            {
                foreach (string linha in File.ReadAllLines(_caminhoArquivo))
                {
                    string[] partes = linha.Split(';');
                    if (partes.Length != 3) continue;

                    TipoPlaca tipo;
                    double curto;
                    double longo;
                    if (!Enum.TryParse(partes[0], out tipo)) continue;
                    if (!double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out curto)) continue;
                    if (!double.TryParse(partes[2], NumberStyles.Float, CultureInfo.InvariantCulture, out longo)) continue;

                    var medida = new MedidaPlaca(curto, longo, true);
                    if (MedidasDisponiveis(tipo).Any(m => m.Equals(medida))) continue;

                    _personalizadas.Add(new KeyValuePair<TipoPlaca, MedidaPlaca>(tipo, medida));
                }
            }
            catch (Exception)
            {
                // Arquivo ilegivel: segue so com as medidas de fabrica, sem interromper o plugin.
            }
        }

        private void Salvar()
        {
            if (string.IsNullOrEmpty(_caminhoArquivo)) return;

            try
            {
                string pasta = Path.GetDirectoryName(_caminhoArquivo);
                if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

                File.WriteAllLines(
                    _caminhoArquivo,
                    _personalizadas.Select(p => string.Format(
                        CultureInfo.InvariantCulture,
                        "{0};{1};{2}",
                        p.Key,
                        p.Value.LadoCurtoMm,
                        p.Value.LadoLongoMm)));
            }
            catch (Exception)
            {
                // Falha ao salvar nao interrompe o fluxo: a medida vale ate fechar o Revit.
            }
        }

        private static Dictionary<TipoPlaca, RegraFixacaoPlaca> CriarRegras()
        {
            // Gesso para drywall: SINAPI (composicoes de parede drywall) -- parafusos a no maximo
            // 250 mm entre si e ~10 mm da borda; TA 25 na 1a camada. A folga do piso e de 15 mm
            // para todos os tipos (definida pelo projeto; a SINAPI cita 10 mm).
            Func<RegraFixacaoPlaca> gesso = () => new RegraFixacaoPlaca
            {
                EspessuraPadraoMm = 12.5,
                EspacamentoAoLongoDoApoioMm = 250,
                DistanciaDaBordaMm = 10,
                JuntaEntrePlacasMm = 0,
                FolgaDoPisoMm = FolgaDoPisoPadraoMm,
                ParafusoPadrao = "TA 25 mm (ponta agulha, cabeca trombeta)",
                PrecisaValidar = false,
                Fonte = "SINAPI (composicoes de parede drywall); NBR 15758; folga do piso definida pelo projeto"
            };

            return new Dictionary<TipoPlaca, RegraFixacaoPlaca>
            {
                { TipoPlaca.GessoSt, gesso() },
                { TipoPlaca.GessoRu, gesso() },
                { TipoPlaca.GessoRf, gesso() },
                { TipoPlaca.GessoPerforma, gesso() },
                { TipoPlaca.PerformaRu, gesso() },

                // Cimenticia: manual Brasilit (rev. dez/2025) -- ate 12 mm: 300 mm na vertical e
                // 400 mm na horizontal; borda minima 15 mm; junta de 4 a 6 mm (partida em 5 mm);
                // parafuso 4,2 x 32 mm.
                {
                    TipoPlaca.Cimenticia,
                    new RegraFixacaoPlaca
                    {
                        EspessuraPadraoMm = 10,
                        EspacamentoAoLongoDoApoioMm = 300,
                        DistanciaDaBordaMm = 15,
                        JuntaEntrePlacasMm = 5,
                        FolgaDoPisoMm = FolgaDoPisoPadraoMm,
                        ParafusoPadrao = "4,2 x 32 mm autobrocante",
                        PrecisaValidar = false,
                        Fonte = "Manual Placa Cimenticia Brasilit (rev. dez/2025); folga do piso definida pelo projeto"
                    }
                },

                // Glasroc X: so o parafuso (3,5 mm; 25 ou 45 mm) tem fonte. Espacamento, borda e
                // junta ainda sem fonte direta -- partida igual ao gesso. [VALIDAR] no manual Placo.
                {
                    TipoPlaca.GlasrocX,
                    new RegraFixacaoPlaca
                    {
                        EspessuraPadraoMm = 12.5,
                        EspacamentoAoLongoDoApoioMm = 250,
                        DistanciaDaBordaMm = 10,
                        JuntaEntrePlacasMm = 0,
                        FolgaDoPisoMm = FolgaDoPisoPadraoMm,
                        ParafusoPadrao = "Parafuso Glasroc 3,5 x 25 mm",
                        PrecisaValidar = true,
                        Fonte = "Parafuso: catalogo Placo; demais valores sem fonte direta"
                    }
                },

                // OSB: sem fonte direta encontrada. Espessura 11,1 mm e a usada em ensaios de LSF.
                // [VALIDAR] com o fabricante da OSB.
                {
                    TipoPlaca.Osb,
                    new RegraFixacaoPlaca
                    {
                        EspessuraPadraoMm = 11.1,
                        EspacamentoAoLongoDoApoioMm = 150,
                        DistanciaDaBordaMm = 10,
                        JuntaEntrePlacasMm = 3,
                        FolgaDoPisoMm = FolgaDoPisoPadraoMm,
                        ParafusoPadrao = "4,2 x 32 mm autobrocante",
                        PrecisaValidar = true,
                        Fonte = "Sem fonte direta; valores de partida"
                    }
                }
            };
        }
    }
}