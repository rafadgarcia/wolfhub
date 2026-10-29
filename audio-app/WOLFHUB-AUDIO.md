# WolfHub Audio

## O que é

O **WolfHub Audio** é um aplicativo independente para organizar perfis e alternar dispositivos de saída e gravação no Windows. Pode ser executado como `.exe` e continua na área de notificação quando a janela é fechada.

## Janela principal

A sidebar lista perfis e diferencia o perfil selecionado do perfil aplicado ao Windows. O painel mostra três seletores: saída principal, saída secundária e dispositivo de gravação. Os nomes dos padrões atuais do Windows aparecem na parte inferior e também têm tooltip com o nome completo.

Escolher dispositivos no formulário cria um rascunho. **Salvar perfil** grava os identificadores no XML local, sem mudar os padrões do Windows. Ao trocar ou excluir um perfil com alterações não salvas, o aplicativo oferece salvar, descartar ou cancelar.

### Saída principal

O botão **Definir principal** aplica a saída principal do perfil e também o dispositivo de gravação configurado, quando disponível.

### Saída secundária

O botão **Salvar secundária** grava a escolha no perfil. Salvar não ativa automaticamente essa saída; ela fica disponível para alternância pela bandeja.

### Dispositivo de gravação

O botão **Definir gravação** pede confirmação antes de alterar o padrão de captura do Windows. Enquanto a janela está visível, o card abre um fluxo WASAPI compartilhado no microfone selecionado e calcula um nível de pico a partir dos pacotes recebidos. Os samples são descartados após o cálculo; WolfHub Audio não grava nem armazena áudio. Ao ocultar a janela para a bandeja, a captura é encerrada. Se o acesso for bloqueado, o medidor aparece desativado com uma dica e o botão **Abrir permissões**, que abre `ms-settings:privacy-microphone`. Ative **Permitir que aplicativos da área de trabalho acessem o microfone** e reinicie o WolfHub Audio. Erros de driver sem suporte, endpoint invalidado e formato incompatível são apresentados como estados distintos. Endpoints desconectados ou inativos aparecem como indisponíveis.

### Testar saída

O botão **Testar** pede confirmação e envia um tom curto diretamente ao endpoint escolhido usando uma sessão WASAPI. Assim, o som do teste não depende do dispositivo padrão do Windows e não altera temporariamente as saídas do sistema. Endpoints indisponíveis ou formatos não suportados apresentam erro em vez de redirecionar o teste para outra saída.

## Bandeja do Windows

- **Clique simples:** consulta a saída e o microfone atuais.
- **Duplo clique:** alterna entre a saída principal e a secundária do perfil aplicado. A ação é bloqueada quando o perfil não tem duas saídas válidas.
- **Botão direito > Perfis:** lista os perfis; escolher um aplica a saída principal e o microfone disponíveis.
- **Abrir WolfHub Audio:** abre a janela de configuração.
- **Consultar áudio atual:** mostra os endpoints padrão atuais.
- **Alternar saída principal / secundária:** executa a mesma alternância do duplo clique.
- **Testar saída atual:** testa a saída padrão após confirmação.
- **Sair:** encerra o processo em vez de mantê-lo na bandeja.

## Configurações avançadas

A página **Configurações globais** reúne duas ações de dispositivos (**Gerenciar apelidos** e **Gerenciar visibilidade**), a preferência **Iniciar com o Windows** e **Reiniciar como administrador**. Essas opções não pertencem a nenhum perfil; o aplicativo não inicia elevado por padrão.

**Gerenciar visibilidade** lista endpoints de saída e gravação, com tipo, estado de atividade, estado de visibilidade registrado pelo WolfHub e indicação de padrão atual/uso no perfil selecionado. A chave chama a mesma API de política de áudio existente e pede confirmação; algumas máquinas podem solicitar elevação pelo UAC. Saídas ativas têm ação **Testar**, com confirmação; gravações não mostram um teste fictício. O estado que o WolfHub altera é salvo como campo XML opcional, então arquivos antigos continuam sendo carregados. Mudanças feitas diretamente em outras ferramentas do Windows podem não estar refletidas na lista, e o resultado da API de visibilidade pode variar por versão do Windows e driver.

Apelidos alteram somente como o endpoint aparece no WolfHub Audio. A lista global não depende do perfil para aplicar apelidos ou visibilidade; a indicação **Usado no perfil atual** serve apenas como contexto.

## Persistência e compatibilidade

Perfis, apelidos e preferência de inicialização são guardados em:

```text
%LOCALAPPDATA%\WolfHubAudio\settings.xml
```

O formato XML existente é mantido. O identificador novo que diferencia perfil selecionado do aplicado é opcional na leitura de configurações antigas; instalações antigas começam com o perfil anteriormente selecionado como aplicado.

O aplicativo enumera endpoints Core Audio e mantém seus identificadores reais. Se um dispositivo salvo for desconectado ou mudar de identificador, a interface informa que está indisponível e permite selecionar outro.

## Limitações

- Apelidos não renomeiam dispositivos globalmente no Windows.
- Não há controles de volume ou sliders nesta versão.
- A disponibilidade das operações de visibilidade e troca de padrão depende das interfaces de áudio do Windows.
- Compilar não valida fisicamente o som, o ícone da bandeja ou escalas de DPI. Faça esses testes manualmente no computador de destino.

## Compilar

Na raiz do projeto, execute em Windows PowerShell 5.1:

```powershell
.\audio-app\build.ps1
```

O executável será criado em `audio-app/dist/WolfHubAudio.exe`. Para validar enumeração e persistência sem alterar os padrões do Windows:

```powershell
.\audio-app\dist\WolfHubAudio.exe --self-test
```
