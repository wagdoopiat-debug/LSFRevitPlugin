
## [ADR-012] Identidade dos elementos estruturais por painel


A auditoria anterior estava incorreta ao concluir que a estrutura nao era gerada por painel. O StructureGenerationService ja percorre os paineis (PainelizacaoService), distribui montantes com WallFraming.CalcularPontosMontantesNoIntervalo e gera guias e reforcos com referencia ao numero do painel para cada elemento. Portanto, "estrutura por painel" ja existe.


A lacuna correta e a persistencia confiavel da identidade dos elementos estruturais em relacao ao painel. Hoje essa relacao e transportada, em boa parte, pelo texto no campo Comentarios do DirectShape. Para tornar essa identidade a fonte de verdade BIM, sem quebrar compatibilidade:


1. Os limites dos paineis continuam no Wall, em Extensible Storage (schema LSFPainelizacaoParede). PainelInfo e derivado.
2. Os elementos estruturais terao, na Etapa 1, Extensible Storage com ParedeId, NumeroPainel, Tipo e Perfil. A identidade do painel permanece (ParedeId, NumeroPainel) -- nao ha UUID proprio de painel neste momento.
3. Os Comentarios permanecem gravados com o mesmo texto, como informacao humana e fallback para elementos antigos sem o novo schema. Elementos legados continuam sendo lidos pelo caminho legado.
4. Placas ja trabalham painel a painel a partir da fonte de paineis do Wall; parafusos, hoje, dependem da leitura da estrutura. A mudanca e incremental, respeitando o principio de compatibilidade: parede nao painelizada continua identica.


Casos de abertura:
- Inteiramente dentro de um painel: comportamento mantido.
- Exatamente sobre a divisa: comportamento mantido na Etapa 1.
- Atravessando divisa: caso conhecido e pendente de regra especifica.


Travamento interno: nao e K-bracing diagonal. Futuramente sera guia + montante interno no painel (aproximadamente 36 cm para modulacao de 40 cm e 56 cm para modulacao de 60 cm). Nao sera implementado nesta etapa. Comments nao deve ser tratado como banco de dados; a migracao para Extensible Storage e progressiva e compatível com elementos existentes.




## [ADR-013] Persistência de identidade das placas por painel (Extensible Storage)

- Persistência em Extensible Storage: schema `LSFPertencenciaPlacas`, GUID `c148a621-c5af-420a-8610-6973e97354d1`.
- Campos: `ParedeId`, `NumeroPainel`, `Face`, `TipoPlaca`, `Orientacao`.
- Os comentários das placas permanecem no formato legado (`LSF-PLACA | ...`). ES é fonte persistente adicional; leitores existentes (`PlacasExistentes`, `QuantitativoService`) continuam lendo comentários nesta etapa (migração ES-first + fallback fica para etapa futura).
- `PlacasService.CriarPecas` grava ES após criação do DirectShape, sem alterar paginação, cortes, juntas, sobras, aproveitamento ou geometria.
- Codec legado em `IdentidadePlaca` para compatibilidade e testes de domínio (12 novos testes).
- Compatibilidade retroativa preservada: placas existentes sem ES continuam reconhecidas pelos comentários. Não há migração em massa.


## [ADR-014] Montagem da trelica, configurações do caixão e chapas gusset (Lote B)

Decisões da missão autônoma consolidando testes reais do Revit 2021 (Lote B):

1. **Montagem da trelica (B2)**: enum `MontagemTrelica {Encaixada, BackToBack}` em `Domain/Laje/ComposicaoViga.cs`, independente do tipo de trelica. `SubPerfisMontagem()` gera uma peca unica (Encaixada) ou duas em costas-com-costas por peca (BackToBack, almas juntas, abas para fora, largura transversal 2×AbaMm). Aplicado a banzos e web members via `TrelicaBuilder`. Persistencia `montagemTrelica=` (backward compat).

2. **Configuracoes do caixao (B3)**: enum `ComposicaoCaixaoTipo {FrenteAFrente, Encaixados}`. FrenteAFrente: almas coaxiais, aberturas opostas (largura = AbaMm). Encaixados: almas separadas por AbaMm, abas do mesmo lado (largura = AbaMm). As duas NAO sao geometricamente equivalentes (diferem em espelhamento e offsets), comprovado por testes de dominio. `ComposicaoViga.SubPerfis(tipo, perfil, caixao)`.

3. **Chapas gusset opcionais (B4)**: `ParametrosLaje.GussetPlacas` (padrao false). `Domain/Laje/GussetLaje.cs` (puro, sem Revit DB) calcula posicoes nos nos da trelica (deduplicados por tolerancia 2mm); `Geometry/GussetBuilder.cs` extrui solidos na normal horizontal do plano da trelica. Valores geometricos de partida (80x80x1.5mm) — nao dimensionados estruturalmente. `PapelElementoLaje.Gusset`, `PapelElemento.GussetLaje` (cor cinza azul), `ResultadoGeracaoLaje.ChapasGussetCriadas`.

Nenhum GUID de schema alterado; nenhum schema novo. `Domain` segue sem `Autodesk.Revit.DB`.


## [ADR-015] Travamento das trelicas, fitas tensionadas, fixadores com direcao e alinhamento a montantes (Lote C/D)

Decisoes da missao autonoma (Lote C e D), cada uma com a parte geometricamente fundamentada implementada e o que falta documentado:

1. **Membro de borda por tipo de viga (C, 1.7)**: `Domain/Laje/TravamentoLaje.cs` (puro) define `TipoMembroBorda {GuiaBorda, TrelicaTravamento}`. Para `TipoViga=Trelica` a borda passa a ser uma trelica de travamento que recebe as pontas das trelicas nos dois apoios; para os demais tipos permanece a guia "U". `LajeGenerationService.CriarBordas` ramifica por `TravamentoLaje.MembroDeBorda` e cria `PapelElementoLaje.Travamento` (`NomeTravamento = "LSF - Travamento (trelica)"`, material `TravamentoLaje`).

2. **Fitas tensionadas (C, 1.7) — DISPOSICAO PROVISORIA, NAO NORMATIVA**: `Domain/Laje/FitasTensionadasLaje.cs` gera linhas de fita em passo alvo (`PassoPadraoMm=1500`) estritamente dentro do vao, exigindo >=2 vigas. `FitaBuilder` cria o solido (largura 30mm, espessura 1mm). Persistido em `fitasTensionadas=` e `espFitas=`. Opt-in (`FitasTensionadas=false` padrao) e so com vigas trelicadas. O numero/passo e o dimensionamento estrutural das fitas (fios, pre-tensao, ligacoes) NAO estao definidos por norma no repositorio e seguem PENDENTES — documentado como provisorio, sem inventar valores normativos.

3. **Fixadores com direcao (C, 1.8)**: `DirecaoFixador {Vertical, AoLongoDaViga}`. Fixador estrutural orienta-se ao longo da viga (sobre guia/travamento); parafuso de placa e vertical. `FixadorBuilder` passou a extruir a caixa ao longo de uma direcao. Tamanhos permanecem provisorios.

4. **Alinhamento de vigas internas a montantes (D, 1.9)**: `Services/ApoiosLajeService.cs` le paredes LSF sob/ao redor do contorno (reuso de `LeituraEstruturaParedeService`), projeta as posicoes de montante para o eixo v e as injeta em `LayoutVigas` com tolerancia de 50mm (`CriteriosLaje.ToleranciaAlinhamentoMm`, PROVISORIO). A viga de borda NAO se move. Se nao houver montante correspondente, `LayoutVigas` emite aviso e mantem a viga na grade (nunca desloca em silencio). Sem paredes, comportamento identico ao anterior.

Nenhum GUID de schema alterado; nenhum schema novo. `Domain` segue sem `Autodesk.Revit.DB`. Novos `PapelElementoLaje {Travamento, Fita}` e `PapelElemento {TravamentoLaje, FitaLaje}` apenas adicionam valores; regeracao continua removendo por `LajeId` (sem duplicacao).
