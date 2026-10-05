# Checklist de Qualidade - Plugin LSF para Revit

Ultima atualizacao: 05/10/2026

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