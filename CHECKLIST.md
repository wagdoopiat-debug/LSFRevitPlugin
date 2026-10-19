# Checklist de Qualidade - Plugin LSF para Revit

Ultima atualizacao: 14/10/2026

Legenda: [OK] = validado no Revit 2021 | [A CONFIRMAR] = ja enviado, aguardando teste no Revit | [PENDENTE] = ainda nao feito

## Estrutura (montantes, guias, encontros e vaos)

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-001 | Core | Manifesto de registro | Revit carrega o plugin sem erro, aba LSF Modeler aparece | [OK] |
| TC-002 | Geometry | Distribuicao de montantes | Espacamento de 40/60cm respeitado, ultimo vao menor, nunca maior | [OK] |
| TC-003 | Geometry | Alinhamento de extremidade | Face externa do montante da ponta alinhada com o fim da parede | [OK] |
| TC-004 | Structural | Verificacao NBR 14762 | 8 testes unitarios passando (local/distorcional/global/flecha) | [OK] |
| TC-005 | Geometry | Encontro em L (Duplo) | Isolado + par H sem sobreposicao, alinhado com o nucleo real da parede | [OK] |
| TC-006 | Geometry | Encontro em T (Triplo) | 1 montante (parede que chega) + 3 (parede passante), sem overlap | [OK] |
| TC-007 | Geometry | Encontro em + (Quadruplo) | 4 montantes (1a parede) + 2 (parede cruzada), sem duplicar | [OK] |
| TC-008 | Geometry | Deduplicacao de encontros | Rodar nas duas paredes de um encontro nao duplica reforco | [OK] |
| TC-009 | Geometry | Vaos - deteccao | Porta/janela existente detectada com largura/altura/soleira corretos | [OK] |
| TC-010 | Geometry | Vaos - reforco | King/Jack Studs + verga/contra-verga gerados sem sobrepor a parede | [OK] |
| TC-011 | Geometry | Vaos - cripple studs | Montantes curtos acima/abaixo do vao seguindo espacamento normal | [OK] |
| TC-012 | UI | Opcoes de geracao | Janela pede sistema, largura, bitola e espacamento 40/60cm; lembra a ultima escolha; Cancelar funciona | [OK] |
| TC-013 | Geral | Vao maximo | Aviso quando algum vao entre montantes excede o espacamento definido | [OK] |
| TC-014 | UI | Ajuste de vaos | Medidas de cada abertura (largura, soleira, topo) ajustaveis antes de gerar; jacks e cripples seguem o ajuste | [OK] |
| TC-015 | Geometry | Perfil Drywall / Steel Frame | Perfil escolhido aplicado a montantes, guias e reforcos de vao | [OK] |
| TC-016 | Geometry | Cores por papel | Montantes, guias e reforcos de vao com cores diferentes por sistema | [OK] |
| TC-017 | Geometry | Orientacao do perfil em C | Costas com costas nos pares (H, +); alma voltada para o vao nas aberturas; T com montante girado e boca invertida | [OK] |
| TC-018 | Geometry | Guias | Guia inferior e superior geradas; inferior interrompida nos vaos de porta | [OK] |
| TC-019 | Geometry | Guias nos cantos em L | Parede que chega avanca ate o lado de fora da outra; a outra recebe o desconto de meia espessura | [A CONFIRMAR] |
| TC-020 | Geometry | Folga de encaixe | 0,5 cm em cada ponta onde o montante entra na guia | [OK] |
| TC-021 | Services | Regeneracao da parede | Gerar de novo apaga e refaz a estrutura inteira da parede, com um unico Ctrl+Z | [OK] |

## Paineis

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-022 | Core | Botao Painelizar | Usuario clica os pontos onde cada painel termina; paineis gravados na parede | [OK] |
| TC-023 | Services | Geracao por painel | Montantes de extremidade em cada lado da divisa, grade recomecando em cada painel, guias cortadas nas divisas | [OK] |
| TC-024 | Services | Painelizacao desatualizada | Se o comprimento da parede mudou, a painelizacao e ignorada e o plugin avisa | [OK] |

## Placas

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-025 | UI | Escolha de placas | Tipo, medida e orientacao (em pe/deitada) por painel e por face; cadastro de novas medidas | [OK] |
| TC-026 | UI | Refazer por painel | Coluna Refazer: so os paineis marcados sao refeitos; os demais mantem as placas | [OK] |
| TC-027 | Domain | Juntas sobre montantes | Juntas verticais sempre sobre montantes e desencontradas entre fiadas e entre as faces | [OK] |
| TC-028 | Domain | Juntas x aberturas | Juntas afastadas das quinas das aberturas; emendas de preferencia no meio dos vaos | [A CONFIRMAR] |
| TC-029 | Domain | Folga do piso | 15 mm entre a base da placa e o piso | [OK] |
| TC-030 | Services | Junta visivel de 3 mm | Folga de 3 mm entre placas vizinhas na geometria 3D; quantitativo segue nominal | [OK] |
| TC-031 | Domain | Reaproveitamento de sobras | Sobras usadas no projeto inteiro (da maior peca para a menor), cortando so quando necessario | [OK] |
| TC-032 | Domain | Largura minima de 40 cm | Peca vertical (faixa em pe) com no minimo 40 cm, exceto quando porta ou janela esta mais perto; altura minima de fiada 200 mm | [A CONFIRMAR] |
| TC-033 | Services | Placas no canto em L | Uma parede rente ao montante; a outra passa a largura do outro montante + espessura da placa; canto com a placa grande lateral | [A CONFIRMAR] |
| TC-034 | Services | Placas no T e no + | Parede que chega encosta no perfil da que passa; a que passa e interrompida so na face onde a outra chega | [A CONFIRMAR] |
| TC-035 | Services | Codigos e recortes | Codigo <parede>-P<painel>-F<face>-<nn> na Marca e recortes etiquetados (-R<n>) | [OK] |
| TC-036 | Services | Cores por tipo de placa | Cada tipo de placa com cor propria | [A CONFIRMAR] |
| TC-037 | Services | Recortes de porta e janela | Usam a medida automatica da esquadria | [OK] |

## Quantitativo e parafusos

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-038 | UI | Janela do quantitativo | Abas (estrutura, corte, placas, pecas, recortes, parafusos, legenda); Ctrl+C copia para o Excel | [OK] |
| TC-039 | Services | Medida dos recortes | Aba Recortes e coluna na aba Pecas com largura x altura de cada recorte | [OK] |
| TC-040 | Services | CSV do quantitativo | Gera CSV (UTF-8, ponto e virgula, pt-BR) com as mesmas tabelas da janela | [OK] |
| TC-041 | Services | Parafusos da estrutura | 4 por montante na guia; emendas costas com costas e conexoes entre paineis e entre paredes, em zigue-zague, passo 250 mm, borda 12,5 mm | [OK] |
| TC-042 | Services | Parafusos das placas | Linha em cada montante atras da peca e ao longo da guia nas bordas de piso e topo; espacamento e borda da regra de cada tipo | [A CONFIRMAR] |
| TC-043 | Services | Parafusos no 3D | Botao Criar parafusos no 3D cria parafusos visiveis (cabeca saliente); recriar substitui os anteriores | [A CONFIRMAR] |

## Testes automatizados e repositorio

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-044 | Tests | Suite de testes unitarios | rodar-testes.ps1 executa sem falhas | [A CONFIRMAR] |
| TC-045 | Repo | README, .gitignore e LICENSE | Outra pessoa consegue clonar, compilar e rodar seguindo o README | [PENDENTE] |

## Funcionalidades ainda nao feitas

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-046 | Services | Desenhos com placas e recortes | Vistas com placas e recortes etiquetados e cotados; marca do codigo sobre cada placa em corte | [PENDENTE] |
| TC-047 | Domain | Duas camadas de placas | Juntas desencontradas entre camadas; regras de L, T e + com espessura somada | [PENDENTE] |
| TC-048 | Services | Relatorio de rejunte | Metros de junta por tipo de placa, indicando junta entre tipos diferentes | [PENDENTE] |
| TC-049 | Core | Editar parede ja lancada | Alterar parametros de uma parede lancada sem refazer do zero | [PENDENTE] |
| TC-050 | Geometry | Furacao dos perfis | Furos na alma conforme NBR 15253 / manual SF | [PENDENTE] |
| TC-051 | Geometry | Modulo de telhado | Estrutura de telhado | [PENDENTE] |
| TC-052 | Core | Lancar varias paredes | Botao Ativar LSF continua ativo para varias paredes ate trocar de botao | [PENDENTE] |
| TC-053 | Domain | Junta x abertura vertical de janela | Junta de placa nao coincide com a abertura vertical de janela nem repete junta vertical no mesmo montante | [PENDENTE] |
| TC-054 | Services | Codigo do painel em cada elemento | Todo elemento gerado mostra o painel a que pertence (hoje aparece so "painel 1") | [PENDENTE] |
| TC-055 | UI | Selecionar painel | Selecionar um painel e listar seus componentes | [PENDENTE] |
| TC-056 | Domain | Coeficientes da NBR 14762 | Coeficientes conferidos contra a norma oficial (hoje marcados [VALIDAR]) | [PENDENTE] |
| TC-057 | Domain | Regras de fixacao OSB e Glasroc X | Espacamento, borda e junta conferidos com o fabricante (hoje [VALIDAR]) | [PENDENTE] |

## Laje (vigas e guias de borda)

| ID | Modulo | Verificacao | Criterio de Aceitacao | Status |
|---|---|---|---|---|
| TC-058 | Services | Geracao das vigas e guias de borda da laje (T11) | Vigas de perfil simples com ALMA VERTICAL e aba transversal, nas posicoes do layout; guia de borda (secao "U", alma vertical, abas horizontais para dentro) em cada aresta perpendicular as vigas; regenerar a mesma laje (mesmo LajeId) NAO duplica; um unico Ctrl+Z desfaz toda a laje. Medir uma viga com LeitorDeEstrutura.Medir: comprimento = vao (4000 mm no retangulo de teste 6000x4000) e altura da alma = LarguraAlmaMm do perfil (90 mm no perfil padrao). | [PENDENTE] |
| TC-059 | UI | Janela de configuracao da laje | Escolhe sistema, tipo (umida/seca), perfil (sistema/largura/bitola), telha, orientacao (automatica/paralela/perpendicular/manual + bordo de referencia/graus), espacamento e cota; mostra previa (mesmo calculo da geracao, sem criar geometria) e bloqueia a geracao quando a previa tem erro. Validavel por inspecao de codigo (`UI\LajeConfigWindow`); so os sistemas/tipos implementados sao oferecidos. | [PENDENTE] |
| TC-060 | Services | Origem do contorno da laje | Piso existente (contorno externo do topo, furos ignorados com aviso), linhas selecionadas (so retas; forma ciclo, sem ramo/aberto/multiplo laco) e pontos clicados (minimo 3, cota = media dos Z). Erros explicitos quando o contorno nao fecha. E2E visual no Revit 2021 [PENDENTE]. | [PENDENTE] |
| TC-061 | Services | Registro da laje por documento | Criar/editar grava a laje (LajeId, origem, config) no Extensible Storage do ProjectInformation (schema `LSFRegistroLajes`, GUID novo); `ProximoLajeId` = maior+1; sobrevive a apagar os elementos gerados; incompativel/corrompido vira lista vazia, sem lancar. Validado pelos testes de codec; E2E no Revit 2021 [PENDENTE]. | [PENDENTE] |
| TC-062 | Core | Editar/regenerar laje | Botao "Editar laje" lista as lajes registradas, reabre a config preenchida e regenera no MESMO LajeId; se a origem era piso e ele ainda existe, o contorno e relido. Se a geracao nao cria nada, a versao anterior e restaurada (rollback). E2E no Revit 2021 [PENDENTE]. | [PENDENTE] |
| TC-063 | Domain | Catalogo de perfis de laje | So perfis Steel Frame (alma 90/140/200 mm, chapa 0,95/1,25 mm); Drywall e chapa 0,80 mm rejeitados por `ParametrosLaje.Validar()`. Validavel por testes de dominio (`CatalogoPerfisLaje`/`ParametrosLaje`); UI nao oferece os invalidos. | [OK] |
| TC-064 | UI/Domain | Orientacao sem duplicidade | Janela com 3 opcoes (Automatica / Por borda de referencia / Manual) + checkbox "Perpendicular a borda de referencia", mapeadas para `OrientacaoVigasModo` por `OrientacaoHelper`; sem opcao redundante. Validado por testes do mapa. | [OK] |
| TC-065 | Domain | Telhas TP40 0,43/0,50/0,63 | `EspessurasDisponiveisMm` com as 3 bitolas; peso da 0,63 marcado `OrigemValor.Provisorio`; bitola 0,65 continua rejeitada. Validado por testes de dominio. | [OK] |
| TC-066 | Domain/Services | Composicao de viga (PerfilSimples/BackToBack/Trelica/Caixao) | `CatalogoTiposVigaLaje` implementado para PerfilSimples, BackToBack, Trelica e Caixao; `ComposicaoViga` descreve sub-perfis (BackToBack = 2 "C" costas-com-costas espelhados, largura = 2×Aba; Caixao = 2 "C" frente a frente no mesmo eixo, largura = Aba). `ComposicaoTrelica`/`CatalogoTrelica` (Warren/Pratt/Howe/DiagonaisAlternadas/MontantesVerticais) e `Geometry\TrelicaBuilder` geram banzos+montantes+diagonais por painel. Validado por testes de dominio; geracao 3D no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-066B | Domain/Geometry | Perfis C reais na trelica (B1) | `Geometry\TrelicaBuilder.cs` usa a MESMA secao "C" com aba enrijecida do `PerfilSpec` (AbaMm/LarguraAlmaMm/EspessuraChapaMm/LabioMm) para diagonais e montantes, via `StudSolidBuilder.CriarSolidoMontanteHorizontal`, em vez de caixa generica; banzos mantem alma vertical. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-066C | Domain/Services | Montagem da trelica (B2) | `Domain\Laje\ComposicaoViga.SubPerfisMontagem` (Encaixada = peca unica; BackToBack = 2 "C" costas com costas por peca, banzos e web); `ParametrosLaje.MontagemTrelica`; largura transversal considera montagem (`LarguraTransversalMm(tipo, perfil, montagem)`); UI `ComboMontagemTrelica`; persistencia `montagemTrelica=`. Valida por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-066D | Domain/Services | Caixao com duas configuracoes (B3) | `ComposicaoViga.SubPerfis(tipo, perfil, caixao)`: FrenteAFrente (almas coaxiais, aberturas opostas) vs Encaixados (almas separadas por AbaMm, abas do mesmo lado); `LajeVigaBuilder.CriarSolidosViga` com caixao; `ParametrosLaje.ComposicaoCaixao`; UI `PanelCaixao`/`ComboComposicaoCaixao`; persistencia `composicaoCaixao=`. As duas configuracoes NAO sao geometricamente equivalentes (comprovado por testes). Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-066E | Domain/Geometry/Services | Chapas gusset opcionais na trelica (B4) | `Domain\Laje\GussetLaje.cs` (novo, puro: `PosicaoGusset`, `GussetLaje.Calcular` — nos deduplicados por tolerancia 2mm) + `Geometry\GussetBuilder.cs` (novo: `CriarSolidosGussets`, extrusao vertical na normal horizontal). `ParametrosLaje.GussetPlacas` (padrao false); `PapelElementoLaje.Gusset` + `PapelElemento.GussetLaje` (cor); `ResultadoGeracaoLaje.ChapasGussetCriadas` (em TotalCriados); UI `CheckGussetPlacas`; persistencia `gussetPlacas=`. Valores geometricos de partida (80×80×1.5mm) — nao dimensionados estruturalmente. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-067 | Domain/Services | Cobertura OSB e cimenticia | `ComposicaoCobertura` (OSB 15/16/22/25; cimenticia 10/20; OSB+cimenticia em camadas separadas); `CamadasLaje.CalcularSecaComPlacas` empilha e a geracao cria placas planas; `PaginacaoPlacas` parcela em chapas OSB 1200x2400 / cimenticia 1200x2400 com juntas e transposicao alternada. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-068 | Domain/Geometry/Services | Trelica de laje | `Domain\Laje\TrelicaTipo.cs`, `ComposicaoTrelica.cs`, `CatalogoTrelica.cs` (puros) + `Geometry\TrelicaBuilder.cs`; `ParametrosLaje.Trelica`/`AlturaTrelicaMm`/`NumeroPaineisTrelica`/`PainelTrelicaAlvoMm` + `AlturaVigaEfetivaMm()`; trelica e Caixao aceitos em `Validar()` (Caixao implementado por P5); UI (PainelTrelica com esquema vetorial no CanvasTrelica) e previa. Validado por testes de dominio; geracao 3D no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-069 | Domain/Services | Paginacao de placas (obrigatoria, junta fixa 3mm) | `Domain\Laje\PaginacaoPlacas.cs` (chapas 1200 mm, juntas fixas em 3 mm, transposicao alternada por camada, recorte Sutherland-Hodgman para contornos convexos; nao convexo tambem e parcelado por triangulacao + costura das arestas internas (RecortePoligono), com aviso). Paginacao OBRIGATORIA quando `ComposicaoPlacas != Nenhuma` (propriedades `PaginarPlacas`/`JuntaPlacasMm` removidas de `ParametrosLaje`; chaves `paginar`/`juntaPlacas` removidas de `ConfiguracaoLajeTexto`, backward compat). Orientacao: OSB perpendicular as vigas (lado largo transversal), cimenticia paralela (lado largo longitudinal) via `DimensoesOrientadas`/`EhCamadaOSB`. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-070 | Domain/Geometry/Services | Telha TP40 3D | `Domain\Laje\SecaoTelhaTp40.cs` (perfil ondulado puro da chapa; `PerfilInferior`) + `Domain\Laje\TelhamentoTp40.cs` (faixas recortadas pelo contorno via varredura em u, `RecortePoligono.IntervalosV`) + `Geometry\TelhaBuilder.cs` (faixa A-B-C-D extrusada ao longo das vigas); `LajeGenerationService.CriarTelhaEAArgamassa` cria DirectShapes "LSF - Telha" (PapelElementoLaje.Telha), uma por coluna. A telha e recortada pelo contorno real (borda serrilhada em contornos nao retangulares, com aviso); transpasse entre chapas nao e modelado. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-071 | Services/Geometry | Argamassa 3D | `LajeGenerationService.CriarTelhaEAArgamassa` cria a placa plana de argamassa acima da onda (PapelElementoLaje.Argamassa) e, quando `ParametrosLaje.PreencherOndas` (UI: "Preencher as ondas da telha com argamassa"), o enchimento das ondas via `TelhamentoTp40.CalcularEnchimento` (regiao entre a linha de fundo e a crista), uma DirectShape por coluna (PapelElementoLaje.Enchimento). A espessura equivalente confere com `TelhaTp40.EnchimentoOndasM3PorM2`. E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-072 | Domain/Geometry/Services | Fixadores (parafusos) com niveis de detalhe | `Domain\Laje\FixadoresLaje.cs` (NivelDetalheFixadores Oculto/Simplificado/Detalhado; estruturais nas pontas das vigas sobre guias; de placa ao longo das vigas) + `Geometry\FixadorBuilder.cs`; UI (PainelFixadores); contagem na previa/resultado. Na geracao, fixadores fora do contorno sao descartados (`RecortePoligono.Contem`) com aviso. Validado por testes de dominio (posicao, passo, relacao com guias e filtro de contorno); E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-073 | Domain | Utilitarios puros de poligono | `Domain\Laje\RecortePoligono.cs`: area, convexidade, ponto-dentro (com borda), varredura vertical (intervalos em v, convexo ou concavo), recorte Sutherland-Hodgman, triangulacao (ear clipping) e costura de arestas internas (`Costurar`), compartilhado por paginacao e telhamento. Validado por testes de dominio. | [OK] |
| TC-074 | Domain | Telhamento TP40 recortado pelo contorno | `Domain\Laje\TelhamentoTp40.cs`: gera faixas (chapa e enchimento) varrendo o contorno em u, colunas de largura util, avisos para contorno nao retangular e comprimento acima do maximo; `FaixaPerfil`/`ResultadoFaixas`/`PapelFaixa`. A espessura equivalente do enchimento confere com o catalogo. Validado por testes de dominio. | [OK] |
| TC-075 | Domain/Services | Membro de borda por tipo de viga (Lote C, 1.7) | `Domain\Laje\TravamentoLaje.cs` (puro): `TipoMembroBorda {GuiaBorda, TrelicaTravamento}`; `MembroDeBorda(TipoViga)` = TrelicaTravamento para viga trelicada, GuiaBorda para os demais; `Apoios(OrientacaoVigas)` = as duas bordas (UMin/UMax). `LajeGenerationService.CriarBordas` ramifica e cria a trelica de travamento (`PapelElementoLaje.Travamento`, "LSF - Travamento (trelica)"). Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-076 | Domain/Geometry/Services | Fitas tensionadas (Lote C, 1.7) — PROVISORIO/NAO NORMATIVO | `Domain\Laje\FitasTensionadasLaje.cs`: linhas de fita em passo alvo (`PassoPadraoMm=1500`, provisorio) estritamente dentro do vao, exigindo >=2 vigas; `Geometry\FitaBuilder.cs` (30x1 mm). `ParametrosLaje.FitasTensionadas` (opt-in, padrao false, so com viga trelicada) + `EspacamentoFitasTensionadasMm`; persistencia `fitasTensionadas=`/`espFitas=`; `PapelElementoLaje.Fita`/`PapelElemento.FitaLaje`; `ResultadoGeracaoLaje.FitasCriadas`; UI `CheckFitasTensionadas`/`PanelFitas`. O dimensionamento estrutural das fitas (fios/pre-tensao/ligacoes) NAO esta implementado (pendencia). Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-077 | Domain/Geometry | Fixadores com direcao (Lote C, 1.8) | `DirecaoFixador {Vertical, AoLongoDaViga}` em `FixadoresLaje`; fixador estrutural orientado ao longo da viga (sobre guia/travamento), parafuso de placa vertical; `Geometry\FixadorBuilder.cs` extrude a caixa ao longo da direcao informada. Tamanhos permanecem provisorios. Validado por testes de dominio; E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-078 | Domain/Services | Alinhamento de vigas internas a montantes (Lote D, 1.9) | `Services\ApoiosLajeService.cs` le paredes LSF sob/ao redor do contorno (reuso de `LeituraEstruturaParedeService`) e projeta posicoes de montante no eixo v; `LayoutVigas.Calcular` alinha vigas internas dentro da tolerancia (`CriteriosLaje.ToleranciaAlinhamentoMm = 50mm`, provisorio); a viga de borda NAO se move; sem montante correspondente o servico emite aviso e a viga permanece na grade (nunca desloca em silencio). Propagado por `PreviaLaje`, `LajeConfigWindow`, `LajeCommand`/`EditarLajeCommand`. Validado por testes de dominio (alinhamento/tolerancia/ausencia/aviso); E2E visual no Revit 2021 [PENDENTE]. | [A CONFIRMAR] |
| TC-079 | Domain | Identidade da laje inclui Travamento e Fita | `PapelElementoLaje {Travamento, Fita}` no codec `IdentidadeLajeCodec` (round-trip preservado); regeracao continua removendo por `LajeId` (sem duplicacao). Validado por testes de dominio. | [OK] |