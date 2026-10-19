# LAJE - Referência técnica e Lote 1 (T01 + T02)

Arquivo: docs\LAJE_REFERENCIA_E_LOTE1.md
Data: 13/10/2026. Contrato de requisitos: docs\LAJE.md (rev. 2).
Escopo deste documento: referência técnica (seções 1 a 6), implementação do Lote 1 (seção 7) e status do Lote 2 (seção 10). Os demais lotes (Quantitativo, Cargas, geometria 3D, integração) NÃO estão implementados.

## 0. Legenda e limites desta referência

Não tive acesso ao repositório. Só vi o que o usuário colou nesta conversa: StudSolidBuilder.cs, GuiaBuilder.cs, StudDirectShapeFactory.cs, SecaoPerfil.cs, PerfilSpec.cs, Nbr14762Calculator.cs, LsfMateriais (arquivo de materiais), LeitorDeEstrutura.cs e ROADMAP.md. Tudo o que não está nesses arquivos é desconhecido.

Marcadores usados:
- [COD] confirmado em código colado nesta conversa.
- [DOC] citado no ROADMAP.md ou na mensagem do usuário; o código correspondente não foi visto.
- [PROPOSTO] decisão minha; não existe no repositório, que eu saiba.
- [VERIFICAR] depende de arquivo que não vi.

Validação feita: nenhum compilador C# estava disponível, então o código da seção 7 NÃO foi compilado. A lógica geométrica (área, autointerseção) e os números dos testes foram conferidos com um equivalente em Python. O compilador e o xUnit do projeto são a verificação final (seção 8).

Correção da especificação rev. 2: o caminho "Tests\LSFRevitPlugin.Domain.Tests" que citei era suposição minha, não fato. Tratar como [VERIFICAR].

## 1. Padrões efetivamente confirmados

| Item | Marcador | Evidência |
|---|---|---|
| Namespaces: LSFRevitPlugin.Domain.Models (PerfilSpec, SistemaPerfil), LSFRevitPlugin.Domain.Structural (Nbr14762Calculator), LSFRevitPlugin.Geometry, LSFRevitPlugin.Services, LSFRevitPlugin.Core | [COD] | cabeçalhos dos arquivos |
| Pastas Domain\Structural, Geometry, Core | [DOC] | ROADMAP.md |
| Pasta de PerfilSpec | [VERIFICAR] | só o namespace foi visto |
| net48, LangVersion 8.0, Revit 2021 | [DOC] | mensagem do usuário |
| Domain nunca referencia Autodesk.Revit.DB | [DOC] | ROADMAP.md, "Regras permanentes" |
| Comentários e mensagens em português SEM acentos; XML docs explicando o porquê | [COD] | todos os arquivos .cs colados |
| Classes de modelo com propriedades get/set; fábrica estática CriarPadrao() | [COD] | PerfilSpec |
| Arrays estáticos readonly para listas de valores (LargurasDrywallMm etc.) | [COD] | PerfilSpec |
| Texto formatado com CultureInfo.InvariantCulture | [COD] | PerfilSpec.DescricaoResumo |
| Resultado de verificação com Aprovado, ModoCritico, TaxaUtilizacao, Observacao | [COD] | Nbr14762Calculator |
| Avisos como List<string> passada por parâmetro | [COD] | LeitorDeEstrutura.Ler(doc, avisos) |
| Contornos 2D como List de tuplas nomeadas: (double U, double V) e (double V, double Z) | [COD] | uso em StudSolidBuilder e GuiaBuilder |
| Testes xUnit, 56 passando, script rodar-testes.ps1 | [DOC] | ROADMAP.md e mensagem do usuário |
| Projeto de testes: nome, pasta, namespace, como enxerga o Domain | [VERIFICAR] | nada visto |
| Estilo (SDK ou antigo) dos .csproj | [VERIFICAR] | nada visto |

## 2. Lacunas (arquivos que ainda não vi)

Para os lotes seguintes, não para o Lote 1:
- .csproj do plugin e do projeto de testes; um arquivo de teste de Domain existente.
- RevitVersionAdapter, SecaoMontante, SecaoGuia, PerfilLSF.
- StructureGenerationService, ResultadoGeracao.
- IdentidadeEstrutural, PertencenciaPainelService, EstruturaTexto.
- App.cs, Command.cs, PreferenciasGeracao, OpcoesGeracaoWindow (.cs e .xaml).
- QuantitativoService, QuantitativoWindow, ParafusosService, Paginador, catálogo de placas.

O Lote 1 foi escrito para depender apenas de PerfilSpec (visto) e do .NET. Os dois únicos pontos de integração com o projeto são o .csproj e o projeto de testes (seção 8).

## 3. Convenções (todas [PROPOSTO] salvo indicação)

Namespaces:
- Domínio da laje: LSFRevitPlugin.Domain.Laje.
- Testes: LSFRevitPlugin.Domain.Tests.Laje (ajustar ao namespace real do projeto de testes).

Unidades e eixos:
- Todo valor numérico do Domain\Laje está em milímetros, e o nome termina em Mm, Mm2, M2, M3PorM2 ou KgM2. O Domain nunca usa unidade interna do Revit; a conversão fica em Geometry/Services via RevitVersionAdapter [DOC].
- Ponto 2D (Ponto2DMm): X para a direita, Y para cima, no mesmo referencial XY do projeto Revit. A cota Z é um número separado (CotaMm).
- Eixos da viga: u ao longo da direção da viga; v perpendicular, igual a u girado +90 graus (anti-horário) [COD: BasisZ.CrossProduct(dir) em StudSolidBuilder e GuiaBuilder, e normal = (-dir.Y, dir.X) em LeitorDeEstrutura].

Contornos:
- ContornoLaje guarda os pontos sem repetir o ponto de fechamento, sem pontos consecutivos coincidentes (tolerância 0,001 mm) e SEMPRE em sentido anti-horário. Contorno só descreve a planta; não carrega unidade nem cota.
- Validação: método Validar() devolve List<string> com erros (mensagens sem acento); lista vazia = válido. Mesmo padrão de avisos como List<string> [COD].
- v1: somente linhas retas (lista de pontos), sem arcos.

Tolerâncias:
- 0,001 mm para pontos coincidentes; 1 mm2 de área mínima; 1e-6 para colinearidade (produto vetorial, mm2).
- Contexto Revit [COD, SecaoPerfil]: curvas menores que cerca de 0,8 mm falham em Line.CreateBound. Os lotes de geometria devem respeitar isso.

Linguagem:
- C# 8.0, net48: sem records, sem init, sem "is not", sem new() com tipo alvo. Tuplas e readonly struct podem. Não usar "string?" porque o estado de nullable do projeto é desconhecido [VERIFICAR].

Catálogo e origem dos valores:
- Cada valor do catálogo é um ValorCatalogo imutável com Nome, Valor, Unidade, Origem (Conferido, Referencia, Calculado, Provisorio) e Fonte. Nome, unidade e fonte nunca ficam em branco (o construtor rejeita).
- Conferido = ficha do próprio produto OU decisão explícita do usuário. Referencia = outro fabricante ou norma. Calculado = fórmula sobre outros valores. Provisorio = padrão proposto para validar.
- Valores derivados são propriedades somente leitura, recalculadas a cada leitura: alterar um valor base muda os derivados.
- PendenciasDoCatalogo() lista tudo que não é Conferido.

## 4. Organização no projeto

Caminhos relativos à pasta do projeto do plugin (onde fica Domain\Structural\Nbr14762Calculator.cs [DOC]).

```text
Domain\Laje\                         [PROPOSTO: pasta nova]
    Ponto2DMm.cs
    TiposLaje.cs                     (TipoLaje, TipoVigaLaje)
    ContornoLaje.cs
    ParametrosLaje.cs
    OrigemValor.cs
    ValorCatalogo.cs
    PendenciaCatalogo.cs
    TelhaTp40.cs
    ArgamassaSpec.cs
    TelaSoldadaSpec.cs
    PisoSecoSpec.cs
    CriteriosLaje.cs
    CatalogoLaje.cs
<PROJETO_DE_TESTES>\Laje\            [PROPOSTO: pasta e nome do projeto a confirmar]
    LajeContornoTests.cs
    LajeParametrosTests.cs
    LajeCatalogoTests.cs
```

Lotes seguintes: **Lote 2 implementado** (ver seção 10): Domain\Laje\OrientacaoVigas.cs, LayoutVigas.cs, ResultadoLayoutVigas.cs, CamadasLaje.cs + 3 arquivos de teste. Ainda NÃO implementados: QuantitativoLaje.cs, CargasLaje.cs; Geometry\LajeVigaBuilder.cs; Services\ para leitura, geração e identidade.

## 5. Exemplos de referência (itens 1 e 2 do pedido)

Para não duplicar código (gasto de tokens), os exemplos completos são os próprios arquivos do Lote 1:
- Exemplo 1, modelo de domínio puro com teste xUnit: Ponto2DMm.cs + ContornoLaje.cs + LajeContornoTests.cs (seção 7).
- Exemplo 2, catálogo com valores padrão, origem e testes: ValorCatalogo.cs + TelhaTp40.cs + CatalogoLaje.cs + LajeCatalogoTests.cs (seção 7).

Todo arquivo novo dos lotes seguintes deve copiar o estilo desses: classe pública, XML docs sem acento, mm, erros em List<string>, testes com nomes em português separados por sublinhado.

## 6. Integração futura com geometria e interface do Revit

### 6.1 Fatos confirmados em código

Conversões:
- RevitVersionAdapter.MilimetrosParaInterno(mm) e RevitVersionAdapter.InternoParaCentimetros(valor) existem [COD].
- LeitorDeEstrutura.Mm(valorInterno) devolve milímetros [COD]. Posições do mundo chegam ao Domain por aqui.

Viga horizontal (perfil simples):
- StudSolidBuilder.CriarSolidoMontanteHorizontal(pontoInicial, dirExtrusaoLong, dirSecaoPerp, comprimentoExtrusaoLongInterna, larguraMm, profundidadeMm, materialId, espessuraChapaMm, labioMm) [COD].
- pontoInicial é o CENTRO da seção: o contorno é desenhado em torno dele (centro mais ou menos meia largura e meia profundidade) e a extrusão parte dele [COD]. Para a viga, o Z de pontoInicial é o centro da altura da alma.
- dirSecaoPerp carrega a alma (profundidade = perfil.LarguraAlmaMm); o terceiro eixo carrega a aba (largura = perfil.AbaMm) [COD]. Viga de piso com alma vertical: dirSecaoPerp = XYZ.BasisZ.
- A abertura do "C" depende de StudSolidBuilder.AbrirParaFrenteDaDirecao (campo estático) [COD]; conferir a orientação visual no Revit.
- Unidades: SecaoMontante.ContornoC recebe valores em unidades internas [COD, documentação de CriarContornoC].

Guia:
- GuiaBuilder.CriarSolidoGuia(inicioParede, direcaoParede, comprimentoInterno, perfil, zBase, materialId, abertoParaCima) [COD]. A alma corre na horizontal (eixo v) e as abas na vertical (Z).
- Guia de borda de laje precisa de alma VERTICAL e abas horizontais: mapear de novo os eixos de SecaoGuia.ContornoU, sem duplicar a seção.
- SecaoGuia.ContornoU recebe mm e seus pontos são usados direto como deslocamentos internos [COD, uso em GuiaBuilder]; ele deve converter por dentro [VERIFICAR no arquivo].

Elementos e identidade:
- StudDirectShapeFactory.CriarElementoMontante(doc, solido ou lista de sólidos, nome) cria DirectShape em OST_StructuralFraming e grava o Name [COD].
- Espessura mínima modelada 1,0 mm (SecaoPerfil.EspessuraModeladaInterna); chapas de 0,43 e 0,50 mm aparecem com 1,0 mm no 3D [COD].
- LeitorDeEstrutura.Ler percorre TODOS os DirectShape e mantém os que têm identidade (Extensible Storage via PertencenciaPainelService, ou Comentários via IdentidadeEstrutural.TentarExtrair) [COD].
- Elementos de laje não podem usar o prefixo EstruturaTexto.Prefixo nos Comentários nem o schema atual; senão entram em Ler() e no quantitativo das paredes. Usar schema novo com GUID novo e prefixo próprio.
- Elemento lido traz ParedeId, Painel, Tipo, Perfil e Medida (U0Mm..U1Mm, N0Mm..N1Mm, Z0Mm..Z1Mm, CentroXMm, CentroYMm em mm, EhMontanteVertical) [COD]. Os centros dos montantes alimentam o alinhamento das vigas.

Materiais (armadilhas confirmadas em LsfMateriais):
- MateriaisLsf tem só três propriedades fixas (Montante, Guia, ReforcoVao) [COD].
- LsfMateriais.CorPara usa switch com default: papéis novos em PapelElemento, sem case próprio, caem na cor de ReforcoVao silenciosamente [COD]. Cada novo papel precisa de case nos dois sistemas.
- ObterOuCriar exige Transaction aberta e nomeia o material "LSF_<Sistema>_<Papel>" [COD].

### 6.2 Proposto (não existe no repositório)

- Fronteira de unidades: um serviço em Services\ converte XYZ do Revit em Ponto2DMm (via LeitorDeEstrutura.Mm) e CotaMm (Z absoluta do projeto, em mm). O Domain nunca vê XYZ.
- Contorno a partir de Floor: topo do piso via HostObjectUtils.GetTopFaces e PlanarFace.GetEdgesAsCurveLoops; só Line na v1 (curvas geram erro claro). Alternativa por Sketch (Floor.SketchId): disponibilidade na API 2021 a confirmar.
- Contorno desenhado: UIDocument.Selection.PickPoint em laço, com linhas de pré-visualização.
- Geração: um TransactionGroup, como em StructureGenerationService [DOC: um Ctrl+Z].
- Preferências: novas linhas em PreferenciasGeracao sem quebrar o arquivo atual [VERIFICAR o formato].

### 6.3 Ainda desconhecido (precisa dos arquivos da seção 2)

Como botões são registrados (App.cs), como o comando chama o serviço (Command.cs), formato de ResultadoGeracao, padrão do schema de Extensible Storage, estrutura de QuantitativoService e QuantitativoWindow, e se o Paginador serve para piso seco.

## 7. Lote 1 - arquivos

### 7.1 Domain\Laje\Ponto2DMm.cs

```csharp
using System;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Ponto 2D em milimetros, no plano da planta (X para a direita, Y para cima), nas mesmas
    /// coordenadas XY do projeto Revit. O Domain nao conhece unidades internas do Revit: a
    /// conversao fica na borda (Geometry/Services), via RevitVersionAdapter.
    /// </summary>
    public readonly struct Ponto2DMm : IEquatable<Ponto2DMm>
    {
        public Ponto2DMm(double xMm, double yMm)
        {
            XMm = xMm;
            YMm = yMm;
        }

        public double XMm { get; }

        public double YMm { get; }

        public double DistanciaAte(Ponto2DMm outro)
        {
            double dx = outro.XMm - XMm;
            double dy = outro.YMm - YMm;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>true se os dois pontos distam no maximo a tolerancia (em mm).</summary>
        public bool EstaProximoDe(Ponto2DMm outro, double toleranciaMm)
        {
            return DistanciaAte(outro) <= toleranciaMm;
        }

        public bool Equals(Ponto2DMm outro)
        {
            return XMm == outro.XMm && YMm == outro.YMm;
        }

        public override bool Equals(object obj)
        {
            return obj is Ponto2DMm outro && Equals(outro);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (XMm.GetHashCode() * 397) ^ YMm.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F3}; {1:F3})", XMm, YMm);
        }
    }
}
```

### 7.2 Domain\Laje\TiposLaje.cs

```csharp
namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tipo de laje: seca (piso seco sobre as vigas) ou umida (telha metalica TP40 + argamassa).
    /// </summary>
    public enum TipoLaje
    {
        Seca,
        Umida
    }

    /// <summary>
    /// Tipo de viga da laje. Na v1 so PerfilSimples esta implementado; os demais entram como
    /// variacoes em tarefas proprias.
    /// </summary>
    public enum TipoVigaLaje
    {
        PerfilSimples,
        Caixao,
        BackToBack,
        Trelica
    }
}
```

### 7.3 Domain\Laje\ContornoLaje.cs

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Contorno da laje na planta: poligono simples fechado, de lados retos, em milimetros.
    /// Ao construir, remove o ponto de fechamento repetido e pontos consecutivos coincidentes,
    /// e deixa os pontos SEMPRE em sentido anti-horario (area assinada positiva).
    /// So descreve a planta: nao carrega cota nem unidade interna do Revit.
    /// </summary>
    public sealed class ContornoLaje
    {
        /// <summary>Distancia (mm) abaixo da qual dois pontos sao considerados o mesmo.</summary>
        public const double ToleranciaMm = 0.001;

        /// <summary>Area minima (mm2) para o contorno ser considerado valido.</summary>
        public const double AreaMinimaMm2 = 1.0;

        // Tolerancia do produto vetorial (mm2) ao decidir se tres pontos sao colineares.
        private const double ToleranciaCruzadoMm2 = 1e-6;

        private readonly ReadOnlyCollection<Ponto2DMm> _pontos;

        public ContornoLaje(IEnumerable<Ponto2DMm> pontos)
        {
            if (pontos == null) throw new ArgumentNullException(nameof(pontos));

            List<Ponto2DMm> lista = LimparPontos(pontos);
            if (CalcularAreaAssinada(lista) < 0.0) lista.Reverse();

            _pontos = lista.AsReadOnly();
            AreaAssinadaMm2 = CalcularAreaAssinada(lista);
        }

        /// <summary>Pontos em sentido anti-horario, sem repetir o ponto de fechamento.</summary>
        public IReadOnlyList<Ponto2DMm> Pontos => _pontos;

        /// <summary>Area com sinal, em mm2. Depois da normalizacao e positiva (ou zero se degenerado).</summary>
        public double AreaAssinadaMm2 { get; }

        public double AreaMm2 => Math.Abs(AreaAssinadaMm2);

        public double AreaM2 => AreaMm2 / 1000000.0;

        public bool EhValido => Validar().Count == 0;

        /// <summary>
        /// Erros que impedem o uso do contorno (lista vazia = valido): menos de 3 pontos
        /// distintos, autointerseccao (cruza, toca ou volta sobre si mesmo) e area quase nula.
        /// </summary>
        public List<string> Validar()
        {
            var erros = new List<string>();

            if (_pontos.Count < 3)
            {
                erros.Add("Contorno precisa de pelo menos 3 pontos distintos.");
                return erros;
            }

            if (TemAutointerseccao())
            {
                erros.Add("Contorno se cruza, se toca ou volta sobre si mesmo (autointerseccao).");
                return erros;
            }

            if (AreaMm2 < AreaMinimaMm2)
            {
                erros.Add("Contorno com area zero ou desprezivel.");
            }

            return erros;
        }

        private static List<Ponto2DMm> LimparPontos(IEnumerable<Ponto2DMm> pontos)
        {
            var resultado = new List<Ponto2DMm>();

            foreach (Ponto2DMm p in pontos)
            {
                if (resultado.Count > 0 && resultado[resultado.Count - 1].EstaProximoDe(p, ToleranciaMm))
                {
                    continue;
                }

                resultado.Add(p);
            }

            // Ponto de fechamento repetido no fim.
            while (resultado.Count > 1 && resultado[0].EstaProximoDe(resultado[resultado.Count - 1], ToleranciaMm))
            {
                resultado.RemoveAt(resultado.Count - 1);
            }

            return resultado;
        }

        /// <summary>
        /// Area com sinal por triangulos a partir do primeiro ponto (valido para poligonos
        /// simples, inclusive concavos). Positiva = anti-horario.
        /// </summary>
        private static double CalcularAreaAssinada(IReadOnlyList<Ponto2DMm> pts)
        {
            if (pts.Count < 3) return 0.0;

            Ponto2DMm origem = pts[0];
            double soma = 0.0;

            for (int i = 1; i < pts.Count - 1; i++)
            {
                double ax = pts[i].XMm - origem.XMm;
                double ay = pts[i].YMm - origem.YMm;
                double bx = pts[i + 1].XMm - origem.XMm;
                double by = pts[i + 1].YMm - origem.YMm;
                soma += ax * by - ay * bx;
            }

            return soma / 2.0;
        }

        private bool TemAutointerseccao()
        {
            int n = _pontos.Count;

            // Arestas consecutivas: so se sobrepoem se uma volta sobre a outra.
            for (int k = 0; k < n; k++)
            {
                if (VoltaSobreSi(_pontos[k], _pontos[(k + 1) % n], _pontos[(k + 2) % n])) return true;
            }

            // Arestas nao consecutivas: nao podem se cruzar nem se tocar.
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue; // consecutivas pelo fechamento

                    if (SegmentosSeTocam(
                        _pontos[i], _pontos[(i + 1) % n],
                        _pontos[j], _pontos[(j + 1) % n]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SegmentosSeTocam(Ponto2DMm p1, Ponto2DMm q1, Ponto2DMm p2, Ponto2DMm q2)
        {
            int o1 = Sinal(Cruzado(p1, q1, p2));
            int o2 = Sinal(Cruzado(p1, q1, q2));
            int o3 = Sinal(Cruzado(p2, q2, p1));
            int o4 = Sinal(Cruzado(p2, q2, q1));

            if (o1 != o2 && o3 != o4) return true;

            if (o1 == 0 && DentroDaCaixa(p1, q1, p2)) return true;
            if (o2 == 0 && DentroDaCaixa(p1, q1, q2)) return true;
            if (o3 == 0 && DentroDaCaixa(p2, q2, p1)) return true;
            if (o4 == 0 && DentroDaCaixa(p2, q2, q1)) return true;

            return false;
        }

        /// <summary>
        /// Duas arestas que se encontram em "comum" voltam uma sobre a outra quando sao
        /// colineares e saem do ponto comum na mesma direcao.
        /// </summary>
        private static bool VoltaSobreSi(Ponto2DMm anterior, Ponto2DMm comum, Ponto2DMm seguinte)
        {
            double v1x = anterior.XMm - comum.XMm;
            double v1y = anterior.YMm - comum.YMm;
            double v2x = seguinte.XMm - comum.XMm;
            double v2y = seguinte.YMm - comum.YMm;

            double cruzado = v1x * v2y - v1y * v2x;
            double escalar = v1x * v2x + v1y * v2y;

            return Sinal(cruzado) == 0 && escalar > 0.0;
        }

        private static double Cruzado(Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            return (b.XMm - a.XMm) * (c.YMm - a.YMm) - (b.YMm - a.YMm) * (c.XMm - a.XMm);
        }

        private static int Sinal(double valor)
        {
            if (valor > ToleranciaCruzadoMm2) return 1;
            if (valor < -ToleranciaCruzadoMm2) return -1;
            return 0;
        }

        private static bool DentroDaCaixa(Ponto2DMm a, Ponto2DMm b, Ponto2DMm c)
        {
            return c.XMm >= Math.Min(a.XMm, b.XMm) - ToleranciaMm
                && c.XMm <= Math.Max(a.XMm, b.XMm) + ToleranciaMm
                && c.YMm >= Math.Min(a.YMm, b.YMm) - ToleranciaMm
                && c.YMm <= Math.Max(a.YMm, b.YMm) + ToleranciaMm;
        }
    }
}
```

### 7.4 Domain\Laje\OrigemValor.cs

```csharp
namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// De onde vem um valor do catalogo. Serve para o usuario ver o que ainda precisa ser conferido.
    /// </summary>
    public enum OrigemValor
    {
        /// <summary>Confirmado em ficha do proprio produto OU decisao explicita do usuario.</summary>
        Conferido,

        /// <summary>Ficha de outro fabricante ou norma, usada na falta da ficha do produto.</summary>
        Referencia,

        /// <summary>Derivado de outros valores por formula.</summary>
        Calculado,

        /// <summary>Padrao proposto, a validar pelo usuario.</summary>
        Provisorio
    }
}
```

### 7.5 Domain\Laje\ValorCatalogo.cs

```csharp
using System;
using System.Globalization;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Um valor do catalogo, imutavel, com nome, unidade, origem e fonte. Nome, unidade e fonte
    /// nunca ficam em branco: o catalogo nao admite valor sem rastro de onde veio.
    /// </summary>
    public sealed class ValorCatalogo
    {
        public ValorCatalogo(string nome, double valor, string unidade, OrigemValor origem, string fonte)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Nome do valor e obrigatorio.", nameof(nome));
            if (string.IsNullOrWhiteSpace(unidade))
                throw new ArgumentException("Unidade do valor e obrigatoria.", nameof(unidade));
            if (string.IsNullOrWhiteSpace(fonte))
                throw new ArgumentException("Fonte do valor e obrigatoria.", nameof(fonte));
            if (double.IsNaN(valor) || double.IsInfinity(valor))
                throw new ArgumentException("Valor deve ser um numero finito.", nameof(valor));

            Nome = nome;
            Valor = valor;
            Unidade = unidade;
            Origem = origem;
            Fonte = fonte;
        }

        public string Nome { get; }

        public double Valor { get; }

        public string Unidade { get; }

        public OrigemValor Origem { get; }

        public string Fonte { get; }

        /// <summary>
        /// Copia com outro valor, origem e fonte (por exemplo, ao trocar um valor de referencia
        /// pelo da ficha oficial). Nome e unidade permanecem.
        /// </summary>
        public ValorCatalogo ComValor(double novoValor, OrigemValor novaOrigem, string novaFonte)
        {
            return new ValorCatalogo(Nome, novoValor, Unidade, novaOrigem, novaFonte);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} = {1:0.###} {2} [{3}; {4}]",
                Nome, Valor, Unidade, Origem, Fonte);
        }
    }
}
```

### 7.6 Domain\Laje\PendenciaCatalogo.cs

```csharp
using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Um valor do catalogo que ainda nao esta Conferido, com o grupo a que pertence
    /// (por exemplo "TelhaTp40").
    /// </summary>
    public sealed class PendenciaCatalogo
    {
        public PendenciaCatalogo(string grupo, ValorCatalogo valor)
        {
            if (string.IsNullOrWhiteSpace(grupo)) throw new ArgumentException("Grupo e obrigatorio.", nameof(grupo));
            if (valor == null) throw new ArgumentNullException(nameof(valor));

            Grupo = grupo;
            Valor = valor;
        }

        public string Grupo { get; }

        public ValorCatalogo Valor { get; }

        /// <summary>Filtra os valores cuja origem nao e Conferido.</summary>
        public static List<PendenciaCatalogo> Filtrar(string grupo, IEnumerable<ValorCatalogo> valores)
        {
            if (valores == null) throw new ArgumentNullException(nameof(valores));

            var lista = new List<PendenciaCatalogo>();
            foreach (ValorCatalogo v in valores)
            {
                if (v.Origem != OrigemValor.Conferido) lista.Add(new PendenciaCatalogo(grupo, v));
            }

            return lista;
        }

        public override string ToString()
        {
            return Grupo + "." + Valor;
        }
    }
}
```

### 7.7 Domain\Laje\TelhaTp40.cs

```csharp
using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Catalogo da telha metalica trapezoidal TP40. A ficha da Pinfer nao foi localizada: os
    /// valores usam referencias de outros fabricantes (ver Origem e Fonte de cada um). Para usar
    /// a ficha oficial, troque so os valores aqui (ComValor), sem mexer em quem consome o catalogo.
    /// Os valores calculados (passo, ondas baixas, enchimento) dependem de catalogo valido:
    /// chame Validar() antes de le-los.
    /// </summary>
    public class TelhaTp40
    {
        public const double Espessura043Mm = 0.43;
        public const double Espessura050Mm = 0.50;

        /// <summary>Espessuras de chapa disponiveis (ficha Pinfer: chapa 28 = 0,43 mm e chapa 26 = 0,50 mm).</summary>
        public static readonly double[] EspessurasDisponiveisMm = { Espessura043Mm, Espessura050Mm };

        public ValorCatalogo LarguraUtilMm { get; set; } = new ValorCatalogo(
            "LarguraUtilMm", 980.0, "mm", OrigemValor.Referencia,
            "Padrao de mercado do TP40/980 (Kingspan Isoeste e outros); ficha Pinfer nao localizada");

        public ValorCatalogo LarguraTotalMm { get; set; } = new ValorCatalogo(
            "LarguraTotalMm", 1032.0, "mm", OrigemValor.Referencia,
            "Kingspan Isoeste (TP40)");

        public ValorCatalogo AlturaOndaMm { get; set; } = new ValorCatalogo(
            "AlturaOndaMm", 40.0, "mm", OrigemValor.Referencia,
            "Nome do perfil (TP40); projeto publico com altura 40 mm");

        public ValorCatalogo OndasAltasPorTelha { get; set; } = new ValorCatalogo(
            "OndasAltasPorTelha", 4.0, "un", OrigemValor.Provisorio,
            "Padrao geometrico usual do TP40/980 (4 ondas); nao encontrado em ficha");

        public ValorCatalogo CristaOndaMm { get; set; } = new ValorCatalogo(
            "CristaOndaMm", 35.0, "mm", OrigemValor.Provisorio,
            "Aproximacao do desenho de perfil TP40 de outro fabricante; usado so em geometria 3D e enchimento");

        public ValorCatalogo BaseOndaAltaMm { get; set; } = new ValorCatalogo(
            "BaseOndaAltaMm", 112.0, "mm", OrigemValor.Provisorio,
            "Aproximacao do desenho de perfil TP40 de outro fabricante; usado so em geometria 3D e enchimento");

        public ValorCatalogo PesoKgM2Chapa043 { get; set; } = new ValorCatalogo(
            "PesoKgM2Chapa043", 3.94, "kg/m2", OrigemValor.Referencia,
            "Hidroaco RT 40/980; a Pinfer informa 3,87 kg/m para 0,43 mm (compativel)");

        public ValorCatalogo PesoKgM2Chapa050 { get; set; } = new ValorCatalogo(
            "PesoKgM2Chapa050", 4.59, "kg/m2", OrigemValor.Referencia,
            "Hidroaco RT 40/980; peso da Pinfer para 0,50 mm nao localizado");

        public ValorCatalogo ComprimentoMaximoMm { get; set; } = new ValorCatalogo(
            "ComprimentoMaximoMm", 12000.0, "mm", OrigemValor.Referencia,
            "Catalogo Isoeste (conferir; Pinfer nao localizado)");

        public ValorCatalogo RecobrimentoLateralOndas { get; set; } = new ValorCatalogo(
            "RecobrimentoLateralOndas", 2.0, "ondas", OrigemValor.Provisorio,
            "Decisao do usuario (duas ondas); na v1 so define a costura de parafusos e nao reduz a largura util");

        public ValorCatalogo EspacamentoParafusoCosturaMm { get; set; } = new ValorCatalogo(
            "EspacamentoParafusoCosturaMm", 300.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio; a confirmar com o usuario");

        /// <summary>Passo da onda = largura util / numero de ondas altas.</summary>
        public ValorCatalogo PassoOndaMm => new ValorCatalogo(
            "PassoOndaMm", LarguraUtilMm.Valor / OndasAltasPorTelha.Valor, "mm", OrigemValor.Calculado,
            "LarguraUtilMm / OndasAltasPorTelha");

        /// <summary>Uma onda baixa por passo, igual ao numero de ondas altas.</summary>
        public ValorCatalogo OndasBaixasPorTelha => new ValorCatalogo(
            "OndasBaixasPorTelha", Math.Round(OndasAltasPorTelha.Valor), "un", OrigemValor.Calculado,
            "Igual ao numero de ondas altas");

        /// <summary>
        /// Argamassa que preenche o espaco entre as ondas altas, em m3 por m2 de laje: area
        /// do retangulo (passo x altura) menos a area do trapezio da onda alta (base e crista),
        /// dividida pelo passo e convertida de mm para m.
        /// </summary>
        public ValorCatalogo EnchimentoOndasM3PorM2
        {
            get
            {
                double passo = LarguraUtilMm.Valor / OndasAltasPorTelha.Valor;
                double altura = AlturaOndaMm.Valor;
                double areaOndaAlta = (BaseOndaAltaMm.Valor + CristaOndaMm.Valor) / 2.0 * altura;
                double espessuraEquivalenteMm = (passo * altura - areaOndaAlta) / passo;

                return new ValorCatalogo(
                    "EnchimentoOndasM3PorM2", espessuraEquivalenteMm / 1000.0, "m3/m2", OrigemValor.Calculado,
                    "((passo x altura) - area do trapezio da onda alta) / passo");
            }
        }

        public static TelhaTp40 CriarPadrao()
        {
            return new TelhaTp40();
        }

        /// <summary>true se a espessura (mm) esta entre as disponiveis.</summary>
        public static bool EspessuraDisponivel(double espessuraMm)
        {
            foreach (double e in EspessurasDisponiveisMm)
            {
                if (Math.Abs(espessuraMm - e) < 1e-6) return true;
            }

            return false;
        }

        /// <summary>Peso da telha (kg/m2) para a espessura informada; lanca excecao se nao houver cadastro.</summary>
        public ValorCatalogo PesoKgM2(double espessuraMm)
        {
            if (Math.Abs(espessuraMm - Espessura043Mm) < 1e-6) return PesoKgM2Chapa043;
            if (Math.Abs(espessuraMm - Espessura050Mm) < 1e-6) return PesoKgM2Chapa050;

            throw new ArgumentOutOfRangeException(
                nameof(espessuraMm), "Espessura de telha sem peso cadastrado (disponiveis: 0,43 e 0,50 mm).");
        }

        /// <summary>Todos os valores, inclusive os calculados. Exige catalogo valido (Validar() sem erros).</summary>
        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[]
            {
                LarguraUtilMm, LarguraTotalMm, AlturaOndaMm, OndasAltasPorTelha, CristaOndaMm, BaseOndaAltaMm,
                PesoKgM2Chapa043, PesoKgM2Chapa050, ComprimentoMaximoMm, RecobrimentoLateralOndas,
                EspacamentoParafusoCosturaMm, PassoOndaMm, OndasBaixasPorTelha, EnchimentoOndasM3PorM2
            };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("TelhaTp40", Valores());
        }

        /// <summary>Erros de coerencia da geometria da telha (lista vazia = coerente).</summary>
        public List<string> Validar()
        {
            var erros = new List<string>();

            if (LarguraUtilMm.Valor <= 0) erros.Add("Largura util da telha deve ser maior que zero.");
            if (AlturaOndaMm.Valor <= 0) erros.Add("Altura da onda deve ser maior que zero.");

            if (OndasAltasPorTelha.Valor < 1)
            {
                erros.Add("Telha precisa de pelo menos 1 onda alta.");
                return erros;
            }

            double passo = LarguraUtilMm.Valor / OndasAltasPorTelha.Valor;

            if (CristaOndaMm.Valor <= 0 || CristaOndaMm.Valor > BaseOndaAltaMm.Valor)
                erros.Add("Crista da onda deve ser positiva e nao maior que a base da onda alta.");

            if (BaseOndaAltaMm.Valor > passo)
                erros.Add("Base da onda alta nao pode ser maior que o passo da onda.");

            return erros;
        }
    }
}
```

### 7.8 Domain\Laje\ArgamassaSpec.cs

```csharp
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Argamassa da laje umida: espessura ACIMA da onda da telha e massa especifica.
    /// O enchimento das ondas vem de TelhaTp40.EnchimentoOndasM3PorM2.
    /// </summary>
    public class ArgamassaSpec
    {
        public ValorCatalogo EspessuraAcimaOndaMm { get; set; } = new ValorCatalogo(
            "EspessuraAcimaOndaMm", 50.0, "mm", OrigemValor.Conferido,
            "Decisao do usuario (5 cm acima da onda)");

        public ValorCatalogo MassaEspecificaKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaKgM3", 2100.0, "kg/m3", OrigemValor.Referencia,
            "NBR 6120 (21 kN/m3, argamassa de cimento e areia); conferir no texto da norma");

        public static ArgamassaSpec CriarPadrao()
        {
            return new ArgamassaSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { EspessuraAcimaOndaMm, MassaEspecificaKgM3 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("ArgamassaSpec", Valores());
        }
    }
}
```

### 7.9 Domain\Laje\TelaSoldadaSpec.cs

```csharp
using System;
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Tela soldada da laje umida: malha POP 20x20 cm, fio 3,4 mm (leve, EQ45), aco CA-60,
    /// painel 2,0 x 3,0 m. O peso e calculado pela geometria da malha.
    /// </summary>
    public class TelaSoldadaSpec
    {
        public string Aco { get; set; } = "CA-60";

        public ValorCatalogo MalhaMm { get; set; } = new ValorCatalogo(
            "MalhaMm", 200.0, "mm", OrigemValor.Conferido, "Decisao do usuario (malha POP 20x20 cm)");

        public ValorCatalogo FioMm { get; set; } = new ValorCatalogo(
            "FioMm", 3.4, "mm", OrigemValor.Conferido, "Decisao do usuario (fio 3,4 mm, leve EQ45)");

        public ValorCatalogo PainelLarguraMm { get; set; } = new ValorCatalogo(
            "PainelLarguraMm", 2000.0, "mm", OrigemValor.Conferido, "Decisao do usuario (painel 2,0 x 3,0 m)");

        public ValorCatalogo PainelComprimentoMm { get; set; } = new ValorCatalogo(
            "PainelComprimentoMm", 3000.0, "mm", OrigemValor.Conferido, "Decisao do usuario (painel 2,0 x 3,0 m)");

        public ValorCatalogo MassaEspecificaAcoKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaAcoKgM3", 7850.0, "kg/m3", OrigemValor.Referencia, "Valor usual para aco");

        public ValorCatalogo SobreposicaoMm { get; set; } = new ValorCatalogo(
            "SobreposicaoMm", 200.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio de uma malha; a confirmar com o usuario");

        /// <summary>
        /// Peso por m2: fios por metro = 1000 / malha, em duas direcoes; massa = comprimento de
        /// fio por m2 x area do fio x massa especifica. Exige malha maior que zero (ver Validar).
        /// </summary>
        public ValorCatalogo PesoKgM2
        {
            get
            {
                double fiosPorMetro = 1000.0 / MalhaMm.Valor;
                double comprimentoFioPorM2 = 2.0 * fiosPorMetro;
                double diametroM = FioMm.Valor / 1000.0;
                double areaFioM2 = Math.PI * diametroM * diametroM / 4.0;
                double kg = comprimentoFioPorM2 * areaFioM2 * MassaEspecificaAcoKgM3.Valor;

                return new ValorCatalogo(
                    "PesoKgM2", kg, "kg/m2", OrigemValor.Calculado,
                    "Fios nas duas direcoes: (2 x 1000 / malha) x area do fio x massa especifica do aco");
            }
        }

        public static TelaSoldadaSpec CriarPadrao()
        {
            return new TelaSoldadaSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[]
            {
                MalhaMm, FioMm, PainelLarguraMm, PainelComprimentoMm,
                MassaEspecificaAcoKgM3, SobreposicaoMm, PesoKgM2
            };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("TelaSoldadaSpec", Valores());
        }

        public List<string> Validar()
        {
            var erros = new List<string>();

            if (MalhaMm.Valor <= 0) erros.Add("Malha da tela deve ser maior que zero.");
            if (FioMm.Valor <= 0) erros.Add("Fio da tela deve ser maior que zero.");
            if (PainelLarguraMm.Valor <= 0 || PainelComprimentoMm.Valor <= 0)
                erros.Add("Dimensoes do painel de tela devem ser maiores que zero.");

            return erros;
        }
    }
}
```

### 7.10 Domain\Laje\PisoSecoSpec.cs

```csharp
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Composicao padrao do piso seco: placa unica sobre as vigas. O tipo real da placa e escolhido
    /// no catalogo de placas existente (tarefa da laje seca); aqui ficam so os padroes de peso.
    /// </summary>
    public class PisoSecoSpec
    {
        public string TipoPlaca { get; set; } = "OSB";

        public ValorCatalogo NumeroCamadas { get; set; } = new ValorCatalogo(
            "NumeroCamadas", 1.0, "un", OrigemValor.Provisorio, "Padrao provisorio: uma camada de placa");

        public ValorCatalogo EspessuraMm { get; set; } = new ValorCatalogo(
            "EspessuraMm", 18.0, "mm", OrigemValor.Provisorio, "Padrao provisorio: OSB 18 mm");

        public ValorCatalogo MassaEspecificaKgM3 { get; set; } = new ValorCatalogo(
            "MassaEspecificaKgM3", 650.0, "kg/m3", OrigemValor.Provisorio,
            "Padrao provisorio: OSB (referencia usual 600 a 650 kg/m3)");

        /// <summary>Peso por m2 = camadas x espessura (m) x massa especifica.</summary>
        public ValorCatalogo PesoKgM2 => new ValorCatalogo(
            "PesoKgM2",
            NumeroCamadas.Valor * (EspessuraMm.Valor / 1000.0) * MassaEspecificaKgM3.Valor,
            "kg/m2", OrigemValor.Calculado,
            "NumeroCamadas x EspessuraMm / 1000 x MassaEspecificaKgM3");

        public static PisoSecoSpec CriarPadrao()
        {
            return new PisoSecoSpec();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { NumeroCamadas, EspessuraMm, MassaEspecificaKgM3, PesoKgM2 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("PisoSecoSpec", Valores());
        }
    }
}
```

### 7.11 Domain\Laje\CriteriosLaje.cs

```csharp
using System.Collections.Generic;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Criterios de verificacao e de layout da laje com padrao proprio (sobrecarga, flecha,
    /// tolerancia de alinhamento e gravidade). Todos provisorios ou de referencia: o usuario os valida.
    /// </summary>
    public class CriteriosLaje
    {
        public ValorCatalogo SobrecargaUsoKnM2 { get; set; } = new ValorCatalogo(
            "SobrecargaUsoKnM2", 1.5, "kN/m2", OrigemValor.Provisorio,
            "NBR 6120, uso residencial; confirmar a categoria de uso");

        public ValorCatalogo LimiteFlechaDivisor { get; set; } = new ValorCatalogo(
            "LimiteFlechaDivisor", 300.0, "un", OrigemValor.Provisorio,
            "Mesmo limite L/300 usado em Nbr14762Calculator.VerificarFlecha");

        public ValorCatalogo ToleranciaAlinhamentoMm { get; set; } = new ValorCatalogo(
            "ToleranciaAlinhamentoMm", 50.0, "mm", OrigemValor.Provisorio,
            "Padrao provisorio para alinhar a viga ao montante mais proximo");

        public ValorCatalogo GravidadeMs2 { get; set; } = new ValorCatalogo(
            "GravidadeMs2", 9.80665, "m/s2", OrigemValor.Referencia,
            "Gravidade normal padrao");

        public static CriteriosLaje CriarPadrao()
        {
            return new CriteriosLaje();
        }

        public IEnumerable<ValorCatalogo> Valores()
        {
            return new[] { SobrecargaUsoKnM2, LimiteFlechaDivisor, ToleranciaAlinhamentoMm, GravidadeMs2 };
        }

        public List<PendenciaCatalogo> Pendencias()
        {
            return PendenciaCatalogo.Filtrar("CriteriosLaje", Valores());
        }
    }
}
```

### 7.12 Domain\Laje\CatalogoLaje.cs

```csharp
using System.Collections.Generic;
using System.Linq;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Ponto unico de acesso aos catalogos da laje (telha, argamassa, tela, piso seco e criterios).
    /// Nenhum valor fica em branco: cada um tem origem e fonte. PendenciasDoCatalogo lista o que
    /// ainda nao esta Conferido, para o usuario ver o que depende de conferencia.
    /// </summary>
    public class CatalogoLaje
    {
        public TelhaTp40 Telha { get; set; } = TelhaTp40.CriarPadrao();

        public ArgamassaSpec Argamassa { get; set; } = ArgamassaSpec.CriarPadrao();

        public TelaSoldadaSpec Tela { get; set; } = TelaSoldadaSpec.CriarPadrao();

        public PisoSecoSpec PisoSeco { get; set; } = PisoSecoSpec.CriarPadrao();

        public CriteriosLaje Criterios { get; set; } = CriteriosLaje.CriarPadrao();

        public static CatalogoLaje CriarPadrao()
        {
            return new CatalogoLaje();
        }

        /// <summary>Todos os valores de todos os grupos, inclusive os calculados.</summary>
        public IEnumerable<ValorCatalogo> TodosOsValores()
        {
            return Telha.Valores()
                .Concat(Argamassa.Valores())
                .Concat(Tela.Valores())
                .Concat(PisoSeco.Valores())
                .Concat(Criterios.Valores());
        }

        /// <summary>Todo valor cuja origem nao e Conferido, com o grupo a que pertence.</summary>
        public List<PendenciaCatalogo> PendenciasDoCatalogo()
        {
            var lista = new List<PendenciaCatalogo>();
            lista.AddRange(Telha.Pendencias());
            lista.AddRange(Argamassa.Pendencias());
            lista.AddRange(Tela.Pendencias());
            lista.AddRange(PisoSeco.Pendencias());
            lista.AddRange(Criterios.Pendencias());
            return lista;
        }

        /// <summary>Erros de coerencia dos catalogos (lista vazia = coerente).</summary>
        public List<string> Validar()
        {
            var erros = new List<string>();
            erros.AddRange(Telha.Validar());
            erros.AddRange(Tela.Validar());
            return erros;
        }
    }
}
```

### 7.13 Domain\Laje\ParametrosLaje.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using LSFRevitPlugin.Domain.Models;

namespace LSFRevitPlugin.Domain.Laje
{
    /// <summary>
    /// Parametros de uma laje a gerar. Validar() devolve os erros que impedem a geracao
    /// (lista vazia = valido), incluindo o escopo da v1: somente viga de perfil simples.
    /// </summary>
    public class ParametrosLaje
    {
        /// <summary>Espacamentos de viga mais usados, em mm (o usuario pode informar outro valor).</summary>
        public static readonly double[] EspacamentosPadraoMm = { 400, 600 };

        public TipoLaje Tipo { get; set; } = TipoLaje.Umida;

        public TipoVigaLaje TipoViga { get; set; } = TipoVigaLaje.PerfilSimples;

        public PerfilSpec Perfil { get; set; } = PerfilSpec.CriarPadrao();

        public double EspacamentoMm { get; set; } = 400.0;

        /// <summary>Direcao das vigas em graus; nulo = automatica (lado mais curto).</summary>
        public double? DirecaoVigasGraus { get; set; }

        public ContornoLaje Contorno { get; set; }

        /// <summary>Cota Z (mm, absoluta no projeto) da face superior do conjunto acabado.</summary>
        public double CotaMm { get; set; }

        /// <summary>Espessura da chapa da telha, em mm; so vale para laje umida. Padrao inicial 0,43.</summary>
        public double EspessuraTelhaMm { get; set; } = TelhaTp40.Espessura043Mm;

        public List<string> Validar()
        {
            var erros = new List<string>();

            if (Contorno == null) erros.Add("Contorno nao informado.");
            else erros.AddRange(Contorno.Validar());

            if (Perfil == null) erros.Add("Perfil nao informado.");

            if (double.IsNaN(EspacamentoMm) || double.IsInfinity(EspacamentoMm) || EspacamentoMm <= 0.0)
                erros.Add("Espacamento deve ser maior que zero.");

            if (DirecaoVigasGraus.HasValue
                && (double.IsNaN(DirecaoVigasGraus.Value) || double.IsInfinity(DirecaoVigasGraus.Value)))
                erros.Add("Direcao das vigas deve ser um numero finito.");

            if (double.IsNaN(CotaMm) || double.IsInfinity(CotaMm))
                erros.Add("Cota deve ser um numero finito.");

            if (TipoViga != TipoVigaLaje.PerfilSimples)
                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Tipo de viga nao implementado na v1: {0} (somente PerfilSimples).", TipoViga));

            if (Tipo == TipoLaje.Umida && !TelhaTp40.EspessuraDisponivel(EspessuraTelhaMm))
            {
                string disponiveis = string.Join(
                    " ou ",
                    Array.ConvertAll(
                        TelhaTp40.EspessurasDisponiveisMm,
                        e => e.ToString("F2", CultureInfo.InvariantCulture)));

                erros.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Espessura de telha invalida ({0:F2} mm); use {1} mm.", EspessuraTelhaMm, disponiveis));
            }

            return erros;
        }
    }
}
```

### 7.14 <PROJETO_DE_TESTES>\Laje\LajeContornoTests.cs

```csharp
using System.Linq;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeContornoTests
    {
        private static Ponto2DMm P(double x, double y)
        {
            return new Ponto2DMm(x, y);
        }

        private static Ponto2DMm[] Retangulo4x6()
        {
            return new[] { P(0, 0), P(6000, 0), P(6000, 4000), P(0, 4000) };
        }

        private static Ponto2DMm[] FormaEmL()
        {
            return new[] { P(0, 0), P(6000, 0), P(6000, 2000), P(2000, 2000), P(2000, 4000), P(0, 4000) };
        }

        private static Ponto2DMm[] FormaEmU()
        {
            return new[]
            {
                P(0, 0), P(6000, 0), P(6000, 4000), P(4000, 4000),
                P(4000, 1000), P(2000, 1000), P(2000, 4000), P(0, 4000)
            };
        }

        [Fact]
        public void Retangulo_4x6_tem_area_24_m2()
        {
            var contorno = new ContornoLaje(Retangulo4x6());

            Assert.Equal(24000000.0, contorno.AreaMm2, 3);
            Assert.Equal(24.0, contorno.AreaM2, 6);
        }

        [Fact]
        public void Contorno_em_L_tem_area_16_m2()
        {
            var contorno = new ContornoLaje(FormaEmL());

            Assert.Equal(16.0, contorno.AreaM2, 6);
        }

        [Fact]
        public void Sentido_horario_vira_anti_horario()
        {
            var horario = new[] { P(0, 0), P(0, 4000), P(6000, 4000), P(6000, 0) };

            var contorno = new ContornoLaje(horario);

            Assert.True(contorno.AreaAssinadaMm2 > 0.0);
            Assert.Equal(24000000.0, contorno.AreaMm2, 3);
            Assert.Equal(4, contorno.Pontos.Count);
        }

        [Fact]
        public void Ponto_de_fechamento_repetido_e_removido()
        {
            var pontos = Retangulo4x6().Concat(new[] { P(0, 0) });

            var contorno = new ContornoLaje(pontos);

            Assert.Equal(4, contorno.Pontos.Count);
            Assert.True(contorno.EhValido);
        }

        [Fact]
        public void Menos_de_tres_pontos_distintos_e_rejeitado()
        {
            var doisPontos = new ContornoLaje(new[] { P(0, 0), P(1000, 0) });
            var tresComRepetido = new ContornoLaje(new[] { P(0, 0), P(1000, 0), P(1000, 0) });

            Assert.Contains(doisPontos.Validar(), e => e.Contains("pelo menos 3 pontos"));
            Assert.Contains(tresComRepetido.Validar(), e => e.Contains("pelo menos 3 pontos"));
        }

        [Fact]
        public void Gravata_e_rejeitada()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(1000, 1000), P(1000, 0), P(0, 1000) });

            Assert.Contains(contorno.Validar(), e => e.Contains("autointerseccao"));
        }

        [Fact]
        public void Pontos_colineares_sao_rejeitados()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(1000, 0), P(2000, 0) });

            Assert.False(contorno.EhValido);
        }

        [Fact]
        public void Vertice_tocando_outra_aresta_e_rejeitado()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(4000, 0), P(4000, 4000), P(2000, 0), P(0, 4000) });

            Assert.Contains(contorno.Validar(), e => e.Contains("autointerseccao"));
        }

        [Fact]
        public void Contorno_desprezivel_e_rejeitado_por_area()
        {
            var contorno = new ContornoLaje(new[] { P(0, 0), P(0.5, 0), P(0, 0.5) });

            Assert.Contains(contorno.Validar(), e => e.Contains("area"));
        }

        [Fact]
        public void Retangulo_L_e_U_sao_validos()
        {
            Assert.True(new ContornoLaje(Retangulo4x6()).EhValido);
            Assert.True(new ContornoLaje(FormaEmL()).EhValido);

            var u = new ContornoLaje(FormaEmU());
            Assert.True(u.EhValido);
            Assert.Equal(18.0, u.AreaM2, 6);
        }
    }
}
```

### 7.15 <PROJETO_DE_TESTES>\Laje\LajeParametrosTests.cs

```csharp
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeParametrosTests
    {
        private static ContornoLaje Retangulo()
        {
            return new ContornoLaje(new[]
            {
                new Ponto2DMm(0, 0), new Ponto2DMm(6000, 0),
                new Ponto2DMm(6000, 4000), new Ponto2DMm(0, 4000)
            });
        }

        private static ParametrosLaje Valido()
        {
            return new ParametrosLaje { Contorno = Retangulo() };
        }

        [Fact]
        public void Parametros_validos_nao_geram_erros()
        {
            Assert.Empty(Valido().Validar());
        }

        [Fact]
        public void Contorno_nulo_gera_erro()
        {
            var p = Valido();
            p.Contorno = null;

            Assert.Contains(p.Validar(), e => e.Contains("Contorno nao informado"));
        }

        [Fact]
        public void Contorno_invalido_propaga_erros()
        {
            var p = Valido();
            p.Contorno = new ContornoLaje(new[] { new Ponto2DMm(0, 0), new Ponto2DMm(1000, 0) });

            Assert.Contains(p.Validar(), e => e.Contains("pelo menos 3 pontos"));
        }

        [Fact]
        public void Perfil_nulo_gera_erro()
        {
            var p = Valido();
            p.Perfil = null;

            Assert.Contains(p.Validar(), e => e.Contains("Perfil nao informado"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-400.0)]
        [InlineData(double.NaN)]
        public void Espacamento_invalido_gera_erro(double espacamentoMm)
        {
            var p = Valido();
            p.EspacamentoMm = espacamentoMm;

            Assert.Contains(p.Validar(), e => e.Contains("Espacamento deve ser maior que zero"));
        }

        [Fact]
        public void Espessura_de_telha_fora_do_catalogo_gera_erro_na_laje_umida()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Umida;
            p.EspessuraTelhaMm = 0.65;

            Assert.Contains(p.Validar(), e => e.Contains("Espessura de telha invalida"));
        }

        [Fact]
        public void Espessura_de_telha_e_ignorada_na_laje_seca()
        {
            var p = Valido();
            p.Tipo = TipoLaje.Seca;
            p.EspessuraTelhaMm = 0.65;

            Assert.Empty(p.Validar());
        }

        [Fact]
        public void Tipo_de_viga_ainda_nao_implementado_gera_erro()
        {
            var p = Valido();
            p.TipoViga = TipoVigaLaje.Caixao;

            Assert.Contains(p.Validar(), e => e.Contains("Tipo de viga nao implementado"));
        }

        [Fact]
        public void Direcao_ou_cota_nao_finitas_geram_erro()
        {
            var direcao = Valido();
            direcao.DirecaoVigasGraus = double.NaN;
            Assert.Contains(direcao.Validar(), e => e.Contains("Direcao das vigas"));

            var cota = Valido();
            cota.CotaMm = double.PositiveInfinity;
            Assert.Contains(cota.Validar(), e => e.Contains("Cota deve ser"));
        }
    }
}
```

### 7.16 <PROJETO_DE_TESTES>\Laje\LajeCatalogoTests.cs

```csharp
using System;
using System.Collections.Generic;
using LSFRevitPlugin.Domain.Laje;
using Xunit;

namespace LSFRevitPlugin.Domain.Tests.Laje
{
    public class LajeCatalogoTests
    {
        // ---------- ValorCatalogo ----------

        [Fact]
        public void ValorCatalogo_sem_fonte_lanca_excecao()
        {
            Assert.Throws<ArgumentException>(
                () => new ValorCatalogo("X", 1.0, "mm", OrigemValor.Provisorio, "  "));
        }

        [Fact]
        public void ComValor_troca_valor_origem_e_fonte_e_mantem_nome_e_unidade()
        {
            var original = new ValorCatalogo("LarguraUtilMm", 980.0, "mm", OrigemValor.Referencia, "Outro fabricante");

            ValorCatalogo novo = original.ComValor(1000.0, OrigemValor.Conferido, "Ficha Pinfer");

            Assert.Equal("LarguraUtilMm", novo.Nome);
            Assert.Equal("mm", novo.Unidade);
            Assert.Equal(1000.0, novo.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, novo.Origem);
            Assert.Equal("Ficha Pinfer", novo.Fonte);
        }

        // ---------- TelhaTp40 ----------

        [Fact]
        public void Telha_passo_e_largura_util_dividida_pelas_ondas_altas()
        {
            var telha = TelhaTp40.CriarPadrao();

            Assert.Equal(245.0, telha.PassoOndaMm.Valor, 6);
            Assert.Equal(OrigemValor.Calculado, telha.PassoOndaMm.Origem);
        }

        [Fact]
        public void Telha_ondas_baixas_igual_ondas_altas()
        {
            Assert.Equal(4.0, TelhaTp40.CriarPadrao().OndasBaixasPorTelha.Valor, 6);
        }

        [Fact]
        public void Telha_enchimento_padrao_e_0_028_m3_por_m2()
        {
            Assert.Equal(0.028, TelhaTp40.CriarPadrao().EnchimentoOndasM3PorM2.Valor, 6);
        }

        [Fact]
        public void Telha_enchimento_muda_com_a_altura_da_onda()
        {
            var telha = TelhaTp40.CriarPadrao();

            telha.AlturaOndaMm = telha.AlturaOndaMm.ComValor(50.0, OrigemValor.Conferido, "Ficha Pinfer");

            Assert.Equal(0.035, telha.EnchimentoOndasM3PorM2.Valor, 6);
        }

        [Fact]
        public void Telha_peso_por_espessura()
        {
            var telha = TelhaTp40.CriarPadrao();

            Assert.Equal(3.94, telha.PesoKgM2(0.43).Valor, 6);
            Assert.Equal(4.59, telha.PesoKgM2(0.50).Valor, 6);
        }

        [Fact]
        public void Telha_peso_de_espessura_sem_cadastro_lanca_excecao()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TelhaTp40.CriarPadrao().PesoKgM2(0.65));
        }

        [Fact]
        public void Telha_padrao_e_coerente()
        {
            Assert.Empty(TelhaTp40.CriarPadrao().Validar());
        }

        [Fact]
        public void Telha_validar_detecta_geometria_incoerente()
        {
            var crista = TelhaTp40.CriarPadrao();
            crista.CristaOndaMm = crista.CristaOndaMm.ComValor(150.0, OrigemValor.Provisorio, "Teste");
            Assert.Contains(crista.Validar(), e => e.Contains("Crista"));

            var semOndas = TelhaTp40.CriarPadrao();
            semOndas.OndasAltasPorTelha = semOndas.OndasAltasPorTelha.ComValor(0.0, OrigemValor.Provisorio, "Teste");
            Assert.Contains(semOndas.Validar(), e => e.Contains("onda alta"));
        }

        // ---------- Argamassa, tela, piso seco, criterios ----------

        [Fact]
        public void Argamassa_padrao_tem_5_cm_acima_da_onda_decididos_pelo_usuario()
        {
            var argamassa = ArgamassaSpec.CriarPadrao();

            Assert.Equal(50.0, argamassa.EspessuraAcimaOndaMm.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, argamassa.EspessuraAcimaOndaMm.Origem);
            Assert.Equal(2100.0, argamassa.MassaEspecificaKgM3.Valor, 6);
        }

        [Fact]
        public void Tela_peso_calculado_e_cerca_de_0_713_kg_por_m2()
        {
            Assert.Equal(0.713, TelaSoldadaSpec.CriarPadrao().PesoKgM2.Valor, 3);
        }

        [Fact]
        public void Tela_valores_decididos_pelo_usuario_sao_conferidos()
        {
            var tela = TelaSoldadaSpec.CriarPadrao();

            Assert.Equal("CA-60", tela.Aco);
            Assert.Equal(200.0, tela.MalhaMm.Valor, 6);
            Assert.Equal(3.4, tela.FioMm.Valor, 6);
            Assert.Equal(2000.0, tela.PainelLarguraMm.Valor, 6);
            Assert.Equal(3000.0, tela.PainelComprimentoMm.Valor, 6);
            Assert.Equal(OrigemValor.Conferido, tela.MalhaMm.Origem);
            Assert.Equal(OrigemValor.Conferido, tela.FioMm.Origem);
            Assert.Equal(OrigemValor.Provisorio, tela.SobreposicaoMm.Origem);
        }

        [Fact]
        public void PisoSeco_peso_calculado_e_11_7_kg_por_m2()
        {
            Assert.Equal(11.7, PisoSecoSpec.CriarPadrao().PesoKgM2.Valor, 6);
        }

        [Fact]
        public void Criterios_padrao()
        {
            var c = CriteriosLaje.CriarPadrao();

            Assert.Equal(1.5, c.SobrecargaUsoKnM2.Valor, 6);
            Assert.Equal(300.0, c.LimiteFlechaDivisor.Valor, 6);
            Assert.Equal(50.0, c.ToleranciaAlinhamentoMm.Valor, 6);
            Assert.Equal(9.80665, c.GravidadeMs2.Valor, 6);
        }

        // ---------- CatalogoLaje ----------

        [Fact]
        public void Catalogo_padrao_e_coerente()
        {
            Assert.Empty(CatalogoLaje.CriarPadrao().Validar());
        }

        [Fact]
        public void Pendencias_nao_incluem_valores_conferidos()
        {
            List<PendenciaCatalogo> pendencias = CatalogoLaje.CriarPadrao().PendenciasDoCatalogo();

            Assert.NotEmpty(pendencias);
            Assert.DoesNotContain(pendencias, p => p.Valor.Origem == OrigemValor.Conferido);
            Assert.DoesNotContain(pendencias, p => p.Valor.Nome == "EspessuraAcimaOndaMm");
            Assert.DoesNotContain(pendencias, p => p.Valor.Nome == "MalhaMm");
        }

        [Fact]
        public void Pendencias_incluem_referencia_provisorio_e_calculado()
        {
            List<PendenciaCatalogo> pendencias = CatalogoLaje.CriarPadrao().PendenciasDoCatalogo();

            Assert.Contains(pendencias, p =>
                p.Grupo == "TelhaTp40" && p.Valor.Nome == "LarguraUtilMm" && p.Valor.Origem == OrigemValor.Referencia);
            Assert.Contains(pendencias, p =>
                p.Valor.Nome == "SobreposicaoMm" && p.Valor.Origem == OrigemValor.Provisorio);
            Assert.Contains(pendencias, p =>
                p.Valor.Nome == "PassoOndaMm" && p.Valor.Origem == OrigemValor.Calculado);
        }

        [Fact]
        public void Todos_os_valores_tem_nome_unidade_e_fonte()
        {
            int total = 0;

            foreach (ValorCatalogo v in CatalogoLaje.CriarPadrao().TodosOsValores())
            {
                Assert.False(string.IsNullOrWhiteSpace(v.Nome));
                Assert.False(string.IsNullOrWhiteSpace(v.Unidade));
                Assert.False(string.IsNullOrWhiteSpace(v.Fonte));
                total++;
            }

            Assert.True(total > 0);
        }
    }
}
```

## 8. Como aplicar e validar (para o agente do opencode)

Regras: copiar os arquivos da seção 7 exatamente como estão, mudando apenas o que o projeto exigir (namespace de testes, lista de arquivos no .csproj). Não criar nem alterar outros arquivos.

1. Criar a pasta Domain\Laje\ e os 13 arquivos de 7.1 a 7.13. Criar a pasta Laje\ no projeto de testes e os 3 arquivos de teste (7.14 a 7.16).
2. Conferir o .csproj do plugin e o do projeto de testes:
   - SDK-style: os arquivos entram sozinhos.
   - Estilo antigo: adicionar um <Compile Include="..."> para cada arquivo novo.
   - Se o projeto de testes liga os arquivos do Domain por link (em vez de referência de projeto), conferir que o padrão cobre Domain\Laje\*.cs, incluindo a nova pasta.
3. Ajustar o namespace dos testes (LSFRevitPlugin.Domain.Tests.Laje) ao padrão dos testes existentes, se for diferente.
4. Build (plugin e testes) e rodar rodar-testes.ps1.
5. Resultado esperado: os 56 testes existentes continuam passando, mais 41 testes novos (3 casos da Theory de espaçamento incluídos), total esperado 97. Se a linha de base não for 56, o total muda de acordo.
6. Qualquer erro de compilação ou teste: devolver a mensagem completa, sem corrigir por conta própria. Os pontos mais prováveis são o namespace dos testes, a lista de arquivos no .csproj e a versão do xUnit (sobrecarga Assert.Equal(double, double, int) e Assert.Contains/DoesNotContain com predicado).
7. Ao final, informar: arquivos criados, total de testes e qualquer ajuste que tenha sido necessário.

## 9. Diferenças em relação à especificação rev. 2

- T02 listava "todos os padrões da seção 6": por isso o Lote 1 inclui PisoSecoSpec, CriteriosLaje e CatalogoLaje, além de TelhaTp40, ArgamassaSpec e TelaSoldadaSpec.
- Origem Conferido passou a incluir decisões explícitas do usuário (espessura da argamassa, tela, fio, painel).
- ParametrosLaje.Validar() rejeita tipos de viga diferentes de PerfilSimples (escopo da v1, alinhado à T20 bloqueada).
- ContornoLaje não valida aresta mínima de 1 mm: se a geometria exigir, entra no lote de geometria (SecaoPerfil já mostra a tolerância de curva curta do Revit).
- O caminho do projeto de testes na rev. 2 era suposição e foi corrigido na seção 0. 

## 10. Lote 2 — OrientacaoVigas, LayoutVigas, ResultadoLayoutVigas, CamadasLaje (implementado)

Data: 14/10/2026. Integrado no repositório. Resultado real desta sessão: `dotnet build LSFRevitPlugin.sln -v minimal` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **229/229 aprovados** (160 do Lote 1 + 69 do Lote 2), 0 falhas, 0 ignorados. Sem regressão.

### 10.1 Arquivos (7)

Domain\Laje\ (produção, sem referência a Autodesk.Revit.DB):
- `LayoutVigas.cs` — grade de vigas (primeira/última sobre as bordas VMin/VMax), alinhamento das vigas internas a apoios dentro da tolerância, resolução determinística de colisões, segmentos por viga (contorno côncavo → vários segmentos), guias de borda, avisos e erros.
- `OrientacaoVigas.cs` — direção automática (vigas ao longo do lado MAIS CURTO do retângulo envolvente; empate = direção da aresta mais longa) ou manual (graus normalizados para [0,180)); eixos u (viga) e v (transversal), `CoordenadaU/CoordenadaV/ParaMundo` e envolvente UMin/UMax/VMin/VMax.
- `ResultadoLayoutVigas.cs` — tipos do resultado: `SegmentoViga`, `VigaLaje`, `GuiaBordaLaje`, `ResultadoLayoutVigas`.
- `CamadasLaje.cs` — camadas verticais da laje (úmida: Argamassa→Telha→Viga; seca: PisoSeco→Viga) por cota; altura da viga = `PerfilSpec.LarguraAlmaMm`.

<PROJETO_DE_TESTES>\Laje\ (testes):
- `LajeLayoutVigasTests.cs` (18 originais + 20 novos)
- `LajeCamadasTests.cs` (11 originais + 7 novos)
- `LajeOrientacaoVigasTests.cs` (novo)

Correção de avisos: o helper de teste `Calcular(...)` passou a receber `double[]? apoios` (o projeto de testes usa `<Nullable>enable</Nullable>`); elimina os 4 avisos CS8625 sem alterar nenhum teste.

### 10.2 Implementado / testado / validado / pendente

- **IMPLEMENTADO** (código integrado e compilando): os 7 arquivos de 10.1.
- **TESTADO** (automação executada no Windows via `powershell.exe`): build 0/0 e **229/229** testes. Sem regressão nos 160 do Lote 1.
- **VALIDADO** (com a evidência disponível): layout, alinhamento, colisão, bordas, segmentos em contorno côncavo, guias de borda, orientação automática/manual e camadas — todos comprovados pelos testes; regra de arquitetura preservada (Domain sem `Autodesk.Revit.DB`).
- **PENDENTE**: (a) o contrato `docs\LAJE.md` (rev. 2) **não existe no repositório nem em artefatos/histórico acessível**, então os requisitos T01..T20 e a aderência exata à rev. 2 NÃO puderam ser validados; (b) decisão de bordas fixas: comportamento implementado/testado, mas confronto com o contrato rev. 2 pendente (ver 10.3); (c) integração no pipeline de geração (`Geometry`/`Services`/`UI`) e demais lotes da laje NÃO existem; (d) revisão de engenharia dos valores provisórios do catálogo (Lote 1) continua pendente.

### 10.3 Decisão sobre bordas (primeira e última viga)

- **Evidência (código):** `LayoutVigas.cs` documenta "as vigas de borda (primeira e última) ficam FIXAS nas extremidades do contorno e nunca são alinhadas a apoios".
- **Evidência (testes):** `Apoio_proximo_da_primeira_borda_nao_move_a_borda`, `Apoio_proximo_da_ultima_borda_nao_move_a_borda`, `Colisao_com_a_primeira_borda_preserva_a_borda_e_remove_a_viga_interna`, `Colisao_com_a_ultima_borda_preserva_a_borda_e_remove_a_viga_interna` e `AssertBordasEUnicidade` (afirma `Vigas[0]`/`Vigas[última]` nas cotas VMin/VMax e não alinhadas).
- **Conclusão:** o comportamento (bordas fixas, nunca alinhadas) está **implementado e testado**.
- **Porém:** como o contrato rev. 2 está ausente, a afirmação de que isso é um **desvio** do contrato (registrada no relatório do autor) NÃO pode ser confirmada aqui. Fica **PENDENTE** de confronto com `docs\LAJE.md`. Este documento, o código e os testes estão alinhados entre si; o alinhamento com o contrato depende do texto ausente — que NÃO foi inventado.

## 11. Lote 2 (continuação) — treliça, fixadores, paginação, telha e argamassa 3D (implementado)

Data: 16/10/2026. Integrado no repositório. Resultado real desta sessão: `dotnet build LSFRevitPlugin.sln` → **0 avisos / 0 erros**; `.\rodar-testes.ps1` → **345/345 aprovados**, 0 falhas, 0 ignorados. Sem regressão.

### 11.1 Arquivos novos

- `Domain\Laje\TrelicaTipo.cs`, `ComposicaoTrelica.cs` (membros: banzo superior, banzo inferior, montante, diagonal), `CatalogoTrelica.cs`.
- `Domain\Laje\FixadoresLaje.cs` (`NivelDetalheFixadores`, `TipoFixador`, `PosicaoFixador`, `ResultadoFixadores`).
- `Domain\Laje\PaginacaoPlacas.cs` (`PlacaPaginada`, `ResultadoPaginacao`).
- `Domain\Laje\SecaoTelhaTp40.cs` (perfil ondulado puro).
- `Geometry\TrelicaBuilder.cs`, `Geometry\FixadorBuilder.cs`, `Geometry\TelhaBuilder.cs`.
- Testes: `LajeTrelicaTests`, `LajeFixadoresTests`, `LajePaginacaoTests`, `LajeTelhaTests`.

### 11.2 Arquivos alterados

- `CatalogoTiposVigaLaje.cs` (Treliça implementado; Caixão segue não implementado).
- `ParametrosLaje.cs` (`Trelica`/`AlturaTrelicaMm`/`NumeroPaineisTrelica`/`PainelTrelicaAlvoMm`/`AlturaVigaEfetivaMm()`; `PaginarPlacas`/`JuntaPlacasMm`; `NivelFixadores`/`EspacamentoFixadoresPlacaMm`; validações).
- `CamadasLaje.cs` (usa `AlturaVigaEfetivaMm()`).
- `IdentidadeLaje.cs` (+`PapelElementoLaje.Argamassa/Telha/Parafuso`), `Geometry\LsfMateriais.cs` (+`PapelElemento.TelhaLaje/ArgamassaLaje/ParafusoLaje` + cores).
- `ConfiguracaoLajeTexto.cs` (persistência `trelica`/`alturaTrelica`/`paineisTrelica`/`painelAlvoTrelica`/`fixadores`/`espFixadores`/`paginar`/`juntaPlacas`).
- `Services\LajeGenerationService.cs` (ramo treliça; `CriarTelhaEAArgamassa`; `CriarFixadores`; `CriarCamadas` com paginação) e `Services\ResultadoGeracaoLaje.cs` (`TelhasCriadas`/`ArgamassasCriadas`).
- `UI\LajeConfigWindow.xaml(.cs)` (PainelTrelica, PainelFixadores, CheckPaginar/TextJunta).
- `Domain\Laje\PreviaLaje.cs` (treliça, fixadores, `ChapasPlacas`, `ChapasTelha`).
- `Geometry\LajeVigaBuilder.cs` (sobrecarga de `CriarSolidoPlaca` para polígono/lista de pontos).

### 11.3 Implementado / testado / validado / pendente / NÃO testado

- **Implementado e testado (domínio):** tipos/composição de treliça e contagem de painéis; fixadores (estruturais/de placa, níveis); paginação de placas (convexo/recorte/juntas/transposição; não convexo = placa única + aviso); perfil transversal da telha; estimativas da prévia.
- **Validado por testes:** os itens acima; `Domain` segue sem `Autodesk.Revit.DB`.
- **Pendente (manual, Revit 2021):** geometria 3D de treliça, fixadores, chapas de placa, telha e argamassa; comportamento de regeneração; TC-058..TC-072 [A CONFIRMAR]/[PENDENTE].
- **Limitações explícitas:** a telha cobre o **retângulo envolvente** (recorte pelo contorno exato PENDENTE); o **enchimento das ondas** (argamassa entre telhas) **NÃO é modelado** — só a placa acima da onda; valores provisórios do catálogo seguem marcados como tal (engenharia pendente); Caixão NÃO implementado.
- **Não testado:** execução no Revit 2021 (WSL não roda o Revit) — nenhuma declaração de validação visual.

