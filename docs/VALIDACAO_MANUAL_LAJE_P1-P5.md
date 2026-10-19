# Validacao manual no Revit 2021 — Laje P1..P5

Passos para conferir no Revit 2021 o que a suite de dominio (414/414) nao cobre:
geometria, materiais e E2E. Marcar cada item apos a conferencia.

Pre-requisito: fechar o Revit, `dotnet build LSFRevitPlugin.sln`, reabrir o Revit e carregar
o plugin (aba "LSF Modeler"). Modelo de teste com um piso retangular de ~6000x4000 mm e, se
possivel, outro em L.

## 1. Telha TP40 recortada pelo contorno (P1)

1. "Criar laje" > "Usar piso existente do Revit" > tipo **Umida** > perfil Steel Frame > telha 0,43.
2. Gerar e, em 3D, isolar as "LSF - Telha".
- [ ] Aparecem chapas onduladas; uma DirectShape por coluna (largura util ~980 mm).
- [ ] O contorno retangular nao gera aviso de "retangular".
- [ ] A telha nao passa da borda da laje (recortada); na diagonal/L a borda fica serrilhada e
      aparece o aviso "Contorno nao retangular...".
- [ ] A cota base da telha coincide com a camada Telha; a espessura modelada e ~1 mm (minimo).

## 2. Enchimento das ondas (P2)

1. Com a mesma laje umida, manter "Preencher as ondas da telha com argamassa" marcado; gerar.
2. Desmarcar, regenerar e comparar.
- [ ] Marcado: aparecem solidos "LSF - Enchimento ondas" (cor cinza medio) entre as telhas, sem
      cobrir as cristas.
- [ ] Desmarcado: nao ha solidos de enchimento; so a placa de argamassa acima da onda.
- [ ] Um unico Ctrl+Z desfaz toda a laje (TransactionGroup).

## 3. Caixao (P5)

1. Na config, escolher Tipo de viga **Caixao**; gerar.
- [ ] As vigas sao duas pecas "C" com as aberturas frente a frente, formando um caixao fechado.
- [ ] A largura transversal e igual a aba do perfil (nao 2x aba como BackToBack).
- [ ] Regenerar a mesma laje nao duplica (mesmo LajeId).

## 4. Paginacao de placas em contorno nao convexo (P4)

1. Tipo **Seca** com composicao OSB e "Parcelar placas em chapas" marcado, contorno em L.
- [ ] As placas cobrem a area sem invadir o vazio; aparecem avisos de contorno nao convexo.
- [ ] Nao ha placas sobrepostas nem "pontes" atravessando o entalhe.

## 5. Fixadores (P3)

1. Definir Nivel de fixadores (Simplificado/Detalhado) e gerar.
- [ ] Fixadores estruturais nas pontas das vigas sobre as guias de borda.
- [ ] Fixadores de placa ao longo das vigas no passo pedido.
- [ ] Nenhum fixador flutuando fora do contorno; se houver descarte, aparece o aviso de fixadores
      ignorados.

## 6. Nao regressao

- [ ] Telha 0,65 mm continua rejeitada na validacao.
- [ ] Laje seca sem composicao mantem o piso padrao (OSB 18 mm).
- [ ] Regenerar/editar uma laje existente nao duplica elementos e restaura o anterior em caso de erro.

## 7. Travamento das trelicas (Lote C)

1. Tipo de viga **Trelica**; gerar.
- [ ] Na borda (dois apoios) aparece "LSF - Travamento (trelica)" (uma trelica), e NAO a guia
      "U" de borda.
- [ ] A trelica de travamento recebe as pontas das trelicas nos dois apoios.
2. Comparar com Tipo de viga **PerfilSimples**: a borda volta a ser guia "U".
- [ ] Trocar o tipo de viga troca o membro de borda coerente com a configuracao.

## 8. Fitas tensionadas (Lote C, disposicao PROVISORIA/nao normativa)

1. Com vigas trelicadas, marcar "Fitas tensionadas de travamento entre trelicas"; definir o passo.
- [ ] Aparecem solidos "LSF - Fita tensionada" na cor de fita (amarelo fosco), cruzando as
      trelicas no interior do vao, sem sobrepor as bordas.
- [ ] Desmarcar remove as fitas; regenerar nao duplica (mesmo LajeId).
- [ ] Os controles de fita so aparecem com Tipo de viga = Trelica.
- [ ] Ciente de que a quantidade/passo e geometrica de partida e o dimensionamento estrutural
      NAO esta implementado (pendencia documentada).

## 9. Alinhamento de vigas a montantes (Lote D)

1. Modelo com paredes LSF sob/parede adjacente ao piso, com montantes; criar a laje sobre o piso.
- [ ] Vigas internas proximas (<= 50 mm) da posicao de um montante da parede abaixo se alinham
      a ele; a viga de borda NAO se move.
- [ ] Quando nao ha montante correspondente, aparece aviso "... viga(s) interna(s) sem
      montante/apoio correspondente ..." e a viga permanece na grade (nao desloca em silencio).
- [ ] Sem paredes sob o piso, a geracao segue igual a antes (grade uniforme, sem alinhamento).

## 10. Fixadores com direcao (Lote C/D)

1. Nivel de fixadores Simplificado/Detalhado; gerar (vigas trelicadas e convencionais).
- [ ] Fixador estrutural no encontro viga x guia/travamento, orientado ao longo da viga.
- [ ] Parafuso de placa vertical (atravessando a placa), sem flutuar fora do contorno.
- [ ] Regenerar a mesma laje nao duplica parafusos.
