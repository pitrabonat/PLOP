#!/usr/bin/env python3
"""Writes Assets/Localization/Resources/Localization/{source,de,uk,ru,fr,es,it,pt-BR}.txt from the rows below.

Row: English source, then de, uk, ru, fr, es, it, pt-BR. Plural rows start with "P:" and give the English
"one" form in ONE[...]; translations list their plural forms separated by "|" (one|other, or one|few|many for
uk/ru). Status of every translation: drafted by an AI model (Claude), NOT reviewed by a native speaker.
Run: python3 Tools/Localization/build_tables.py
"""
import os

LANGS = ["de", "uk", "ru", "fr", "es", "it", "pt-BR"]
NAMES = {"de": "German", "uk": "Ukrainian", "ru": "Russian", "fr": "French", "es": "Spanish", "it": "Italian",
         "pt-BR": "Portuguese (Brazil)"}

ROWS = [
    # ---- main menu
    ("Sleepy Puzzle", "Schläfriges Rätsel", "Сонна головоломка", "Сонная головоломка", "Puzzle tout doux", "Puzle dormilón", "Puzzle dormiglione", "Quebra-cabeça soneca"),
    ("Play", "Spielen", "Грати", "Играть", "Jouer", "Jugar", "Gioca", "Jogar"),
    ("Level", "Level", "Рівень", "Уровень", "Niveau", "Nivel", "Livello", "Fase"),
    ("Level {0}", "Level {0}", "Рівень {0}", "Уровень {0}", "Niveau {0}", "Nivel {0}", "Livello {0}", "Fase {0}"),
    ("Gift", "Geschenk", "Подарунок", "Подарок", "Cadeau", "Regalo", "Regalo", "Presente"),
    ("Tomorrow", "Morgen", "Завтра", "Завтра", "Demain", "Mañana", "Domani", "Amanhã"),
    ("Daily", "Täglich", "Щодня", "Ежедневно", "Du jour", "Diario", "Del giorno", "Diário"),
    ("Daily Gift", "Tagesgeschenk", "Щоденний подарунок", "Ежедневный подарок", "Cadeau du jour", "Regalo diario", "Regalo del giorno", "Presente diário"),
    ("Daily Puzzle", "Tagesrätsel", "Головоломка дня", "Головоломка дня", "Puzzle du jour", "Puzle diario", "Puzzle del giorno", "Desafio diário"),
    ("Levels", "Level", "Рівні", "Уровни", "Niveaux", "Niveles", "Livelli", "Fases"),
    ("Studio", "Studio", "Студія", "Студия", "Studio", "Estudio", "Studio", "Estúdio"),
    ("Dream Dust", "Traumstaub", "Пил снів", "Пыль снов", "Poussière de rêve", "Polvo de sueños", "Polvere dei sogni", "Pó dos sonhos"),
    ("+{0} Dream Dust", "+{0} Traumstaub", "+{0} Пилу снів", "+{0} Пыли снов", "+{0} Poussière de rêve", "+{0} Polvo de sueños", "+{0} Polvere dei sogni", "+{0} Pó dos sonhos"),
    ("Next gift tomorrow", "Nächstes Geschenk morgen", "Наступний подарунок завтра", "Следующий подарок завтра", "Prochain cadeau demain", "Próximo regalo mañana", "Prossimo regalo domani", "Próximo presente amanhã"),
    ("Daily puzzle unavailable", "Tagesrätsel nicht verfügbar", "Головоломка дня недоступна", "Головоломка дня недоступна", "Puzzle du jour indisponible", "Puzle diario no disponible", "Puzzle del giorno non disponibile", "Desafio diário indisponível"),
    ("New in Studio: {0}", "Neu im Studio: {0}", "Нове в студії: {0}", "Новое в студии: {0}", "Nouveau au Studio : {0}", "Nuevo en el Estudio: {0}", "Novità nello Studio: {0}", "Novidade no Estúdio: {0}"),
    # ---- HUD
    ("Settings", "Einstellungen", "Налаштування", "Настройки", "Réglages", "Ajustes", "Impostazioni", "Configurações"),
    ("Home", "Start", "Головна", "Главная", "Accueil", "Inicio", "Home", "Início"),
    ("Undo", "Zurück", "Скасувати", "Отменить", "Annuler", "Deshacer", "Annulla", "Desfazer"),
    ("Restart", "Neustart", "Заново", "Заново", "Recommencer", "Reiniciar", "Ricomincia", "Reiniciar"),
    ("Hint", "Tipp", "Підказка", "Подсказка", "Indice", "Pista", "Aiuto", "Dica"),
    ("Hint {0}", "Tipp {0}", "Підказка {0}", "Подсказка {0}", "Indice {0}", "Pista {0}", "Aiuto {0}", "Dica {0}"),
    ("P:{0} moves", "{0} Zug|{0} Züge", "{0} хід|{0} ходи|{0} ходів", "{0} ход|{0} хода|{0} ходов", "{0} coup|{0} coups", "{0} movimiento|{0} movimientos", "{0} mossa|{0} mosse", "{0} movimento|{0} movimentos", "{0} move"),
    ("Perfect!", "Perfekt!", "Ідеально!", "Идеально!", "Parfait !", "¡Perfecto!", "Perfetto!", "Perfeito!"),
    ("Sweet Dreams!", "Süße Träume!", "Солодких снів!", "Сладких снов!", "Fais de beaux rêves !", "¡Dulces sueños!", "Sogni d'oro!", "Bons sonhos!"),
    ("Perfect: {0}", "Perfekt: {0}", "Ідеал: {0}", "Идеал: {0}", "Parfait : {0}", "Perfecto: {0}", "Perfetto: {0}", "Perfeito: {0}"),
    ("Menu", "Menü", "Меню", "Меню", "Menu", "Menú", "Menu", "Menu"),
    ("Next Level", "Nächstes Level", "Наступний рівень", "Следующий уровень", "Niveau suivant", "Siguiente nivel", "Livello successivo", "Próxima fase"),
    ("Replay", "Nochmal", "Ще раз", "Ещё раз", "Rejouer", "Repetir", "Rigioca", "Repetir"),
    ("On", "An", "Увімк.", "Вкл.", "Oui", "Sí", "Sì", "Sim"),
    ("Off", "Aus", "Вимк.", "Выкл.", "Non", "No", "No", "Não"),
    ("Oops, we're stuck!", "Hoppla, wir stecken fest!", "Ой, ми застрягли!", "Ой, мы застряли!", "Oups, on est coincés !", "¡Ups, estamos atascados!", "Ops, siamo bloccati!", "Opa, ficamos presos!"),
    ("There's no way to get everyone home from here.", "Von hier kommen nicht mehr alle nach Hause.", "Звідси вже не вийде довести всіх додому.", "Отсюда уже не получится довести всех домой.", "Impossible de ramener tout le monde à la maison d'ici.", "Desde aquí no se puede llevar a todos a casa.", "Da qui non si riesce a portare tutti a casa.", "Daqui não dá para levar todos para casa."),
    ("Undo Move", "Zug zurück", "Скасувати хід", "Отменить ход", "Annuler le coup", "Deshacer", "Annulla mossa", "Desfazer"),
    ("Restart Level", "Level neu starten", "Почати заново", "Начать заново", "Recommencer", "Reiniciar nivel", "Ricomincia livello", "Reiniciar fase"),
    ("Keep playing", "Weiterspielen", "Грати далі", "Играть дальше", "Continuer", "Seguir jugando", "Continua a giocare", "Continuar"),
    ("Choose a level", "Level wählen", "Оберіть рівень", "Выберите уровень", "Choisis un niveau", "Elige un nivel", "Scegli un livello", "Escolha uma fase"),
    ("Worlds", "Welten", "Світи", "Миры", "Mondes", "Mundos", "Mondi", "Mundos"),
    ("Close", "Schließen", "Закрити", "Закрыть", "Fermer", "Cerrar", "Chiudi", "Fechar"),
    ("Previous", "Zurück", "Назад", "Назад", "Précédent", "Anterior", "Indietro", "Anterior"),
    ("More", "Weiter", "Далі", "Далее", "Suite", "Más", "Altri", "Mais"),
    ("Level {0} · Perfect", "Level {0} · Perfekt", "Рівень {0} · Ідеально", "Уровень {0} · Идеально", "Niveau {0} · Parfait", "Nivel {0} · Perfecto", "Livello {0} · Perfetto", "Fase {0} · Perfeita"),
    ("Level {0} · Done", "Level {0} · Geschafft", "Рівень {0} · Пройдено", "Уровень {0} · Пройден", "Niveau {0} · Fini", "Nivel {0} · Hecho", "Livello {0} · Fatto", "Fase {0} · Feita"),
    ("Level {0} · Locked", "Level {0} · Gesperrt", "Рівень {0} · Закрито", "Уровень {0} · Закрыт", "Niveau {0} · Verrouillé", "Nivel {0} · Bloqueado", "Livello {0} · Bloccato", "Fase {0} · Bloqueada"),
    ("Levels {0}–{1}", "Level {0}–{1}", "Рівні {0}–{1}", "Уровни {0}–{1}", "Niveaux {0}–{1}", "Niveles {0}–{1}", "Livelli {0}–{1}", "Fases {0}–{1}"),
    ("Done", "Fertig", "Готово", "Готово", "OK", "Listo", "Fatto", "Pronto"),
    ("Music", "Musik", "Музика", "Музыка", "Musique", "Música", "Musica", "Música"),
    ("Effects", "Effekte", "Ефекти", "Эффекты", "Effets", "Efectos", "Effetti", "Efeitos"),
    ("Ambience", "Atmosphäre", "Атмосфера", "Атмосфера", "Ambiance", "Ambiente", "Ambiente", "Ambiente"),
    ("Master mute", "Alles stumm", "Вимкнути звук", "Выключить звук", "Couper le son", "Silenciar todo", "Silenzia tutto", "Silenciar tudo"),
    ("Frame rate", "Bildrate", "Частота кадрів", "Частота кадров", "Images par seconde", "Fotogramas", "Frame rate", "Taxa de quadros"),
    ("Haptics", "Vibration", "Вібрація", "Вибрация", "Vibrations", "Vibración", "Vibrazione", "Vibração"),
    ("Reduced motion", "Weniger Bewegung", "Менше руху", "Меньше движения", "Animations réduites", "Menos movimiento", "Meno movimento", "Menos movimento"),
    ("Restore Defaults", "Standard wiederherstellen", "Скинути налаштування", "Сбросить настройки", "Réglages par défaut", "Restablecer", "Ripristina", "Restaurar padrão"),
    ("Language", "Sprache", "Мова", "Язык", "Langue", "Idioma", "Lingua", "Idioma"),
    # ---- Worlds
    ("Resume Level {0}", "Weiter mit Level {0}", "Продовжити рівень {0}", "Продолжить уровень {0}", "Reprendre le niveau {0}", "Continuar nivel {0}", "Riprendi livello {0}", "Continuar fase {0}"),
    ("Resume", "Weiter", "Продовжити", "Продолжить", "Reprendre", "Continuar", "Riprendi", "Continuar"),
    ("Levels {0}–{1} · {2}/{3}", "Level {0}–{1} · {2}/{3}", "Рівні {0}–{1} · {2}/{3}", "Уровни {0}–{1} · {2}/{3}", "Niveaux {0}–{1} · {2}/{3}", "Niveles {0}–{1} · {2}/{3}", "Livelli {0}–{1} · {2}/{3}", "Fases {0}–{1} · {2}/{3}"),
    ("Current", "Aktuell", "Поточний", "Текущий", "En cours", "Actual", "Attuale", "Atual"),
    ("Locked", "Gesperrt", "Закрито", "Закрыто", "Verrouillé", "Bloqueado", "Bloccato", "Bloqueado"),
    ("World unlocked", "Welt freigeschaltet", "Світ відкрито", "Мир открыт", "Monde débloqué", "Mundo desbloqueado", "Mondo sbloccato", "Mundo liberado"),
    ("Cozy Nest", "Kuschelnest", "Затишне гніздечко", "Уютное гнёздышко", "Nid douillet", "Nido acogedor", "Nido accogliente", "Ninho aconchegante"),
    ("Dewdrop Garden", "Tautropfengarten", "Сад росинок", "Сад росинок", "Jardin de rosée", "Jardín de rocío", "Giardino di rugiada", "Jardim do orvalho"),
    ("Rainy Window", "Regenfenster", "Дощове вікно", "Дождливое окно", "Fenêtre pluvieuse", "Ventana lluviosa", "Finestra di pioggia", "Janela chuvosa"),
    ("Twilight Attic", "Dämmerdachboden", "Сутінкове горище", "Сумеречный чердак", "Grenier du soir", "Ático del crepúsculo", "Soffitta al tramonto", "Sótão do crepúsculo"),
    ("Moonlit Clouds", "Mondscheinwolken", "Місячні хмари", "Лунные облака", "Nuages de lune", "Nubes de luna", "Nuvole di luna", "Nuvens ao luar"),
    ("Frosted Pond", "Frostteich", "Замерзлий ставок", "Замёрзший пруд", "Étang givré", "Estanque helado", "Stagno ghiacciato", "Lago congelado"),
    ("Breezy Meadow", "Windige Wiese", "Вітряна галявина", "Ветреный луг", "Prairie venteuse", "Prado ventoso", "Prato ventoso", "Campo de brisa"),
    ("Ember Hollow", "Glutmulde", "Жаринна долинка", "Тлеющая лощина", "Creux des braises", "Hondonada de brasas", "Conca delle braci", "Vale das brasas"),
    ("Prism Observatory", "Prisma-Sternwarte", "Призмова обсерваторія", "Призменная обсерватория", "Observatoire prisme", "Observatorio prisma", "Osservatorio prisma", "Observatório prisma"),
    # ---- hints and recovery
    ("Undo or Restart first.", "Erst zurück oder neu starten.", "Спершу скасуйте хід або почніть заново.", "Сначала отмените ход или начните заново.", "Annule ou recommence d'abord.", "Primero deshaz o reinicia.", "Prima annulla o ricomincia.", "Primeiro desfaça ou reinicie."),
    ("Looking for a hint…", "Suche einen Tipp…", "Шукаю підказку…", "Ищу подсказку…", "Je cherche un indice…", "Buscando una pista…", "Cerco un aiuto…", "Procurando uma dica…"),
    ("A hint costs {0} Dream Dust. You have {1}.", "Ein Tipp kostet {0} Traumstaub. Du hast {1}.", "Підказка коштує {0} Пилу снів. У вас {1}.", "Подсказка стоит {0} Пыли снов. У вас {1}.", "Un indice coûte {0} Poussière de rêve. Tu en as {1}.", "Una pista cuesta {0} de Polvo de sueños. Tienes {1}.", "Un aiuto costa {0} Polvere dei sogni. Ne hai {1}.", "Uma dica custa {0} de Pó dos sonhos. Você tem {1}."),
    ("P:Keep trying: after {0} more tries the first hint here is free.",
     "Weiter so: nach {0} weiteren Versuch ist der erste Tipp hier gratis.|Weiter so: nach {0} weiteren Versuchen ist der erste Tipp hier gratis.",
     "Не здавайтеся: ще {0} спроба — і перша підказка тут безкоштовна.|Не здавайтеся: ще {0} спроби — і перша підказка тут безкоштовна.|Не здавайтеся: ще {0} спроб — і перша підказка тут безкоштовна.",
     "Не сдавайтесь: ещё {0} попытка — и первая подсказка здесь бесплатна.|Не сдавайтесь: ещё {0} попытки — и первая подсказка здесь бесплатна.|Не сдавайтесь: ещё {0} попыток — и первая подсказка здесь бесплатна.",
     "Continue : encore {0} essai et le premier indice ici est gratuit.|Continue : encore {0} essais et le premier indice ici est gratuit.",
     "Sigue intentando: tras {0} intento más, la primera pista aquí es gratis.|Sigue intentando: tras {0} intentos más, la primera pista aquí es gratis.",
     "Continua: dopo {0} altro tentativo il primo aiuto qui è gratis.|Continua: dopo altri {0} tentativi il primo aiuto qui è gratis.",
     "Continue tentando: depois de mais {0} tentativa, a primeira dica aqui é grátis.|Continue tentando: depois de mais {0} tentativas, a primeira dica aqui é grátis.",
     "Keep trying: after {0} more try the first hint here is free."),
    ("−{0} Dream Dust · {1} left", "−{0} Traumstaub · noch {1}", "−{0} Пилу снів · залишилось {1}", "−{0} Пыли снов · осталось {1}", "−{0} Poussière de rêve · il en reste {1}", "−{0} Polvo de sueños · quedan {1}", "−{0} Polvere dei sogni · ne restano {1}", "−{0} Pó dos sonhos · restam {1}"),
    ("This one is on the house.", "Dieser geht aufs Haus.", "Ця — за рахунок закладу.", "Эта — за счёт заведения.", "Celui-ci est offert.", "Esta va por cuenta de la casa.", "Questo lo offre la casa.", "Essa é por conta da casa."),
    ("Try swiping up.", "Wisch mal nach oben.", "Спробуйте провести вгору.", "Попробуйте провести вверх.", "Essaie de glisser vers le haut.", "Prueba a deslizar hacia arriba.", "Prova a scorrere in alto.", "Tente deslizar para cima."),
    ("Try swiping down.", "Wisch mal nach unten.", "Спробуйте провести вниз.", "Попробуйте провести вниз.", "Essaie de glisser vers le bas.", "Prueba a deslizar hacia abajo.", "Prova a scorrere in basso.", "Tente deslizar para baixo."),
    ("Try swiping left.", "Wisch mal nach links.", "Спробуйте провести вліво.", "Попробуйте провести влево.", "Essaie de glisser vers la gauche.", "Prueba a deslizar hacia la izquierda.", "Prova a scorrere a sinistra.", "Tente deslizar para a esquerda."),
    ("Try swiping right.", "Wisch mal nach rechts.", "Спробуйте провести вправо.", "Попробуйте провести вправо.", "Essaie de glisser vers la droite.", "Prueba a deslizar hacia la derecha.", "Prova a scorrere a destra.", "Tente deslizar para a direita."),
    ("No route from here. Try Undo.", "Von hier gibt es keinen Weg. Versuch's mit Zurück.", "Звідси немає шляху. Спробуйте скасувати хід.", "Отсюда нет пути. Попробуйте отменить ход.", "Aucun chemin d'ici. Essaie Annuler.", "No hay camino desde aquí. Prueba a deshacer.", "Nessuna strada da qui. Prova Annulla.", "Não há caminho daqui. Tente desfazer."),
    ("No hint available. Try Undo.", "Kein Tipp verfügbar. Versuch's mit Zurück.", "Підказки немає. Спробуйте скасувати хід.", "Подсказки нет. Попробуйте отменить ход.", "Pas d'indice disponible. Essaie Annuler.", "No hay pista disponible. Prueba a deshacer.", "Nessun aiuto disponibile. Prova Annulla.", "Nenhuma dica disponível. Tente desfazer."),
    ("P:Challenge · {0} moves", "Herausforderung · {0} Zug|Herausforderung · {0} Züge", "Виклик · {0} хід|Виклик · {0} ходи|Виклик · {0} ходів", "Испытание · {0} ход|Испытание · {0} хода|Испытание · {0} ходов", "Défi · {0} coup|Défi · {0} coups", "Reto · {0} movimiento|Reto · {0} movimientos", "Sfida · {0} mossa|Sfida · {0} mosse", "Desafio · {0} movimento|Desafio · {0} movimentos", "Challenge · {0} move"),
    ("Oops! Too hot!", "Autsch! Zu heiß!", "Ой! Гаряче!", "Ой! Горячо!", "Aïe ! Trop chaud !", "¡Ay! ¡Quema!", "Ahi! Che caldo!", "Ai! Quente demais!"),
    ("Lava is no place for a nap. Undo or try again.", "Lava ist kein Ort für ein Nickerchen. Zurück oder neu versuchen.", "Лава — не місце для сну. Скасуйте хід або спробуйте ще раз.", "Лава — не место для сна. Отмените ход или попробуйте снова.", "La lave, ce n'est pas pour la sieste. Annule ou réessaie.", "La lava no es lugar para una siesta. Deshaz o inténtalo de nuevo.", "La lava non è posto per un pisolino. Annulla o riprova.", "Lava não é lugar para cochilar. Desfaça ou tente de novo."),
    ("Out of moves!", "Keine Züge mehr!", "Ходи закінчились!", "Ходы закончились!", "Plus de coups !", "¡Sin movimientos!", "Mosse finite!", "Sem movimentos!"),
    ("P:This challenge allows {0} moves. Undo or try again.",
     "Diese Herausforderung erlaubt {0} Zug. Zurück oder neu versuchen.|Diese Herausforderung erlaubt {0} Züge. Zurück oder neu versuchen.",
     "Цей виклик дозволяє {0} хід. Скасуйте хід або спробуйте ще раз.|Цей виклик дозволяє {0} ходи. Скасуйте хід або спробуйте ще раз.|Цей виклик дозволяє {0} ходів. Скасуйте хід або спробуйте ще раз.",
     "Это испытание допускает {0} ход. Отмените ход или попробуйте снова.|Это испытание допускает {0} хода. Отмените ход или попробуйте снова.|Это испытание допускает {0} ходов. Отмените ход или попробуйте снова.",
     "Ce défi autorise {0} coup. Annule ou réessaie.|Ce défi autorise {0} coups. Annule ou réessaie.",
     "Este reto permite {0} movimiento. Deshaz o inténtalo de nuevo.|Este reto permite {0} movimientos. Deshaz o inténtalo de nuevo.",
     "Questa sfida consente {0} mossa. Annulla o riprova.|Questa sfida consente {0} mosse. Annulla o riprova.",
     "Este desafio permite {0} movimento. Desfaça ou tente de novo.|Este desafio permite {0} movimentos. Desfaça ou tente de novo.",
     "This challenge allows {0} move. Undo or try again."),
    # ---- tutorial instructions (level data)
    ("Swipe the Plop into its matching Nest.", "Wisch den Plop in sein passendes Nest.", "Проведіть, щоб Плоп потрапив у своє гніздо.", "Проведите, чтобы Плоп попал в своё гнездо.", "Glisse le Plop jusqu'à son nid assorti.", "Desliza el Plop hasta su nido a juego.", "Scorri il Plop fino al suo nido abbinato.", "Deslize o Plop até o ninho da mesma cor."),
    ("One swipe moves every awake Plop.", "Ein Wisch bewegt jeden wachen Plop.", "Один свайп рухає всіх неспаних Плопів.", "Один свайп двигает всех неспящих Плопов.", "Un glissement déplace tous les Plops éveillés.", "Un deslizamiento mueve a todos los Plops despiertos.", "Uno scorrimento muove tutti i Plop svegli.", "Um deslize move todos os Plops acordados."),
    ("Frost: Plops slide until something stops them.", "Frost: Plops rutschen, bis etwas sie stoppt.", "Іній: Плопи ковзають, доки щось їх не зупинить.", "Иней: Плопы скользят, пока что-то их не остановит.", "Givre : les Plops glissent jusqu'à ce que quelque chose les arrête.", "Escarcha: los Plops se deslizan hasta que algo los detiene.", "Brina: i Plop scivolano finché qualcosa li ferma.", "Geada: os Plops deslizam até algo pará-los."),
    ("Breeze fans push Plops the way they point. Dots show how far.", "Windräder schieben Plops in ihre Richtung. Punkte zeigen, wie weit.", "Вітрячки штовхають Плопів, куди вказують. Крапки показують, як далеко.", "Вертушки толкают Плопов туда, куда указывают. Точки показывают, как далеко.", "Les ventilos poussent les Plops dans leur direction. Les points montrent jusqu'où.", "Los ventiladores empujan a los Plops hacia donde apuntan. Los puntos indican cuánto.", "Le ventole spingono i Plop dove puntano. I puntini mostrano quanto lontano.", "Os ventiladores empurram os Plops para onde apontam. Os pontos mostram até onde."),
    ("Lava is too hot! Keep every Plop out.", "Lava ist zu heiß! Halte alle Plops fern.", "Лава надто гаряча! Не пускайте туди жодного Плопа.", "Лава слишком горячая! Не пускайте туда ни одного Плопа.", "La lave est trop chaude ! Garde les Plops à l'écart.", "¡La lava quema! Mantén a los Plops fuera.", "La lava scotta! Tieni fuori ogni Plop.", "A lava é quente demais! Mantenha os Plops longe."),
    ("Loops connect: in one, out the other.", "Schleifen sind verbunden: rein in die eine, raus aus der anderen.", "Петлі з'єднані: в одну зайшов — з іншої вийшов.", "Петли связаны: вошёл в одну — вышел из другой.", "Les boucles sont reliées : on entre par l'une, on sort par l'autre.", "Los aros se conectan: entras por uno y sales por el otro.", "Gli anelli sono collegati: entri da uno, esci dall'altro.", "Os portais se conectam: entra em um, sai pelo outro."),
    # ---- Studio
    ("Body", "Körper", "Тіло", "Тело", "Corps", "Cuerpo", "Corpo", "Corpo"),
    ("Colors", "Farben", "Кольори", "Цвета", "Couleurs", "Colores", "Colori", "Cores"),
    ("Finish", "Oberfläche", "Фактура", "Фактура", "Finition", "Acabado", "Finitura", "Acabamento"),
    ("Face", "Gesicht", "Обличчя", "Лицо", "Visage", "Cara", "Viso", "Rosto"),
    ("Accessories", "Zubehör", "Аксесуари", "Аксессуары", "Accessoires", "Accesorios", "Accessori", "Acessórios"),
    ("Nest", "Nest", "Гніздо", "Гнездо", "Nid", "Nido", "Nido", "Ninho"),
    ("Board", "Brett", "Поле", "Поле", "Plateau", "Tablero", "Tavola", "Tabuleiro"),
    ("Equipped", "Angelegt", "Вдягнено", "Надето", "Équipé", "Equipado", "Equipaggiato", "Equipado"),
    ("Equip", "Anlegen", "Вдягнути", "Надеть", "Équiper", "Equipar", "Equipaggia", "Equipar"),
    ("Tap to confirm", "Zum Bestätigen tippen", "Торкніться, щоб підтвердити", "Нажмите для подтверждения", "Touche pour confirmer", "Toca para confirmar", "Tocca per confermare", "Toque para confirmar"),
    ("Unlock {0}", "Freischalten {0}", "Відкрити за {0}", "Открыть за {0}", "Débloquer {0}", "Desbloquear {0}", "Sblocca {0}", "Liberar {0}"),
    ("Need {0} more", "Noch {0} nötig", "Потрібно ще {0}", "Нужно ещё {0}", "Il manque {0}", "Faltan {0}", "Ne servono altri {0}", "Faltam {0}"),
    ("Free", "Gratis", "Безкоштовно", "Бесплатно", "Gratuit", "Gratis", "Gratis", "Grátis"),
    ("Owned", "Im Besitz", "Є", "Есть", "Possédé", "Tuyo", "Tuo", "Seu"),
    ("{0} or {1}", "{0} oder {1}", "{0} або {1}", "{0} или {1}", "{0} ou {1}", "{0} o {1}", "{0} o {1}", "{0} ou {1}"),
    ("{0} Dust", "{0} Staub", "{0} пилу", "{0} пыли", "{0} poussière", "{0} de polvo", "{0} polvere", "{0} de pó"),
    ("Wearing now", "Gerade angelegt", "Зараз вдягнено", "Сейчас надето", "Porté en ce moment", "Lo llevas puesto", "Lo indossi ora", "Usando agora"),
    ("Free · tap Equip", "Gratis · tippe auf Anlegen", "Безкоштовно · торкніться «Вдягнути»", "Бесплатно · нажмите «Надеть»", "Gratuit · touche Équiper", "Gratis · toca Equipar", "Gratis · tocca Equipaggia", "Grátis · toque em Equipar"),
    ("Unlocked", "Freigeschaltet", "Відкрито", "Открыто", "Débloqué", "Desbloqueado", "Sbloccato", "Liberado"),
    ("Locked · {0}", "Gesperrt · {0}", "Закрито · {0}", "Закрыто · {0}", "Verrouillé · {0}", "Bloqueado · {0}", "Bloccato · {0}", "Bloqueado · {0}"),
    ("Locked · {0} or {1} Dream Dust", "Gesperrt · {0} oder {1} Traumstaub", "Закрито · {0} або {1} Пилу снів", "Закрыто · {0} или {1} Пыли снов", "Verrouillé · {0} ou {1} Poussière de rêve", "Bloqueado · {0} o {1} de Polvo de sueños", "Bloccato · {0} o {1} Polvere dei sogni", "Bloqueado · {0} ou {1} de Pó dos sonhos"),
    ("Locked · {0} Dream Dust", "Gesperrt · {0} Traumstaub", "Закрито · {0} Пилу снів", "Закрыто · {0} Пыли снов", "Verrouillé · {0} Poussière de rêve", "Bloqueado · {0} de Polvo de sueños", "Bloccato · {0} Polvere dei sogni", "Bloqueado · {0} de Pó dos sonhos"),
    ("Unlocked {0}!", "{0} freigeschaltet!", "Відкрито: {0}!", "Открыто: {0}!", "{0} débloqué !", "¡{0} desbloqueado!", "{0} sbloccato!", "{0} liberado!"),
    ("Not enough Dream Dust", "Nicht genug Traumstaub", "Недостатньо Пилу снів", "Недостаточно Пыли снов", "Pas assez de Poussière de rêve", "No tienes suficiente Polvo de sueños", "Polvere dei sogni insufficiente", "Pó dos sonhos insuficiente"),
    ("Could not unlock, try again", "Freischalten fehlgeschlagen, bitte erneut versuchen", "Не вдалося відкрити, спробуйте ще", "Не удалось открыть, попробуйте ещё", "Impossible de débloquer, réessaie", "No se pudo desbloquear, inténtalo de nuevo", "Sblocco non riuscito, riprova", "Não foi possível liberar, tente de novo"),
    ("P:{0} levels", "{0} Level|{0} Level", "{0} рівень|{0} рівні|{0} рівнів", "{0} уровень|{0} уровня|{0} уровней", "{0} niveau|{0} niveaux", "{0} nivel|{0} niveles", "{0} livello|{0} livelli", "{0} fase|{0} fases", "{0} level"),
    ("P:{0} Perfects", "{0} Perfekt|{0} Perfekte", "{0} ідеал|{0} ідеали|{0} ідеалів", "{0} идеал|{0} идеала|{0} идеалов", "{0} Parfait|{0} Parfaits", "{0} Perfecto|{0} Perfectos", "{0} Perfetto|{0} Perfetti", "{0} Perfeita|{0} Perfeitas", "{0} Perfect"),
    ("P:{0} Worlds", "{0} Welt|{0} Welten", "{0} світ|{0} світи|{0} світів", "{0} мир|{0} мира|{0} миров", "{0} monde|{0} mondes", "{0} mundo|{0} mundos", "{0} mondo|{0} mondi", "{0} mundo|{0} mundos", "{0} World"),
    ("P:{0} Dailies", "{0} Tagesrätsel|{0} Tagesrätsel", "{0} щоденна|{0} щоденні|{0} щоденних", "{0} ежедневная|{0} ежедневные|{0} ежедневных", "{0} puzzle du jour|{0} puzzles du jour", "{0} diario|{0} diarios", "{0} quotidiano|{0} quotidiani", "{0} diário|{0} diários", "{0} Daily"),
    ("P:Complete {0} levels", "Schaffe {0} Level|Schaffe {0} Level", "Пройдіть {0} рівень|Пройдіть {0} рівні|Пройдіть {0} рівнів", "Пройдите {0} уровень|Пройдите {0} уровня|Пройдите {0} уровней", "Termine {0} niveau|Termine {0} niveaux", "Completa {0} nivel|Completa {0} niveles", "Completa {0} livello|Completa {0} livelli", "Complete {0} fase|Complete {0} fases", "Complete {0} level"),
    ("P:Earn {0} Perfects", "Hol {0} Perfekt|Hol {0} Perfekte", "Отримайте {0} ідеал|Отримайте {0} ідеали|Отримайте {0} ідеалів", "Получите {0} идеал|Получите {0} идеала|Получите {0} идеалов", "Obtiens {0} Parfait|Obtiens {0} Parfaits", "Consigue {0} Perfecto|Consigue {0} Perfectos", "Ottieni {0} Perfetto|Ottieni {0} Perfetti", "Ganhe {0} Perfeita|Ganhe {0} Perfeitas", "Earn {0} Perfect"),
    ("P:Finish {0} Worlds", "Schließe {0} Welt ab|Schließe {0} Welten ab", "Завершіть {0} світ|Завершіть {0} світи|Завершіть {0} світів", "Завершите {0} мир|Завершите {0} мира|Завершите {0} миров", "Termine {0} monde|Termine {0} mondes", "Termina {0} mundo|Termina {0} mundos", "Completa {0} mondo|Completa {0} mondi", "Conclua {0} mundo|Conclua {0} mundos", "Finish {0} World"),
    ("P:Solve {0} Dailies", "Löse {0} Tagesrätsel|Löse {0} Tagesrätsel", "Розв'яжіть {0} щоденну|Розв'яжіть {0} щоденні|Розв'яжіть {0} щоденних", "Решите {0} ежедневную|Решите {0} ежедневные|Решите {0} ежедневных", "Résous {0} puzzle du jour|Résous {0} puzzles du jour", "Resuelve {0} diario|Resuelve {0} diarios", "Risolvi {0} quotidiano|Risolvi {0} quotidiani", "Resolva {0} diário|Resolva {0} diários", "Solve {0} Daily"),
    # ---- Sprint 04 advertising (voluntary rewarded ads, consent entry)
    ("Ad", "Werbung", "Реклама", "Реклама", "Pub", "Anuncio", "Annuncio", "Anúncio"),
    ("Watch Ad", "Werbung ansehen", "Переглянути рекламу", "Смотреть рекламу", "Voir la pub", "Ver anuncio", "Guarda annuncio", "Ver anúncio"),
    ("No, thanks", "Nein, danke", "Ні, дякую", "Нет, спасибо", "Non merci", "No, gracias", "No, grazie", "Não, obrigado"),
    ("Ad unavailable", "Keine Werbung verfügbar", "Реклама недоступна", "Реклама недоступна", "Pub indisponible", "Anuncio no disponible", "Annuncio non disponibile", "Anúncio indisponível"),
    ("Bonus Dream Dust", "Bonus-Traumstaub", "Бонусний пил снів", "Бонусная пыль снов", "Poussière de rêve bonus", "Polvo de sueños extra", "Polvere dei sogni bonus", "Pó dos sonhos bônus"),
    ("Watch Ad · +{0} Dream Dust", "Werbung ansehen · +{0} Traumstaub", "Реклама · +{0} Пилу снів", "Реклама · +{0} Пыли снов", "Voir la pub · +{0} Poussière de rêve", "Ver anuncio · +{0} Polvo de sueños", "Guarda annuncio · +{0} Polvere dei sogni", "Ver anúncio · +{0} Pó dos sonhos"),
    ("Watch a short ad to get {0} Dream Dust. Up to {1} bonuses a day.", "Sieh dir eine kurze Werbung an und erhalte {0} Traumstaub. Bis zu {1} Boni pro Tag.", "Перегляньте коротку рекламу й отримайте {0} Пилу снів. До {1} бонусів на день.", "Посмотрите короткую рекламу и получите {0} Пыли снов. До {1} бонусов в день.", "Regarde une courte pub pour obtenir {0} Poussière de rêve. Jusqu'à {1} bonus par jour.", "Mira un anuncio corto para conseguir {0} Polvo de sueños. Hasta {1} bonos al día.", "Guarda un breve annuncio per ottenere {0} Polvere dei sogni. Fino a {1} bonus al giorno.", "Veja um anúncio curto para ganhar {0} Pó dos sonhos. Até {1} bônus por dia."),
    ("Thanks! +{0} Dream Dust", "Danke! +{0} Traumstaub", "Дякуємо! +{0} Пилу снів", "Спасибо! +{0} Пыли снов", "Merci ! +{0} Poussière de rêve", "¡Gracias! +{0} Polvo de sueños", "Grazie! +{0} Polvere dei sogni", "Obrigado! +{0} Pó dos sonhos"),
    ("No reward this time. The ad did not finish.", "Diesmal keine Belohnung. Die Werbung lief nicht bis zum Ende.", "Цього разу без нагороди. Реклама не завершилася.", "В этот раз без награды. Реклама не досмотрена.", "Pas de récompense cette fois. La pub n'est pas allée jusqu'au bout.", "Esta vez no hay recompensa. El anuncio no terminó.", "Nessuna ricompensa questa volta. L'annuncio non è terminato.", "Sem recompensa desta vez. O anúncio não terminou."),
    ("Get a hint", "Tipp holen", "Отримати підказку", "Получить подсказку", "Obtenir un indice", "Obtener una pista", "Ottieni un aiuto", "Pegar uma dica"),
    ("Use {0} Dream Dust", "{0} Traumstaub nutzen", "Витратити {0} Пилу снів", "Потратить {0} Пыли снов", "Utiliser {0} Poussière de rêve", "Usar {0} Polvo de sueños", "Usa {0} Polvere dei sogni", "Usar {0} Pó dos sonhos"),
    ("Watch Ad · Free Hint", "Werbung · Gratis-Tipp", "Реклама · Безкоштовна підказка", "Реклама · Бесплатная подсказка", "Pub · Indice gratuit", "Anuncio · Pista gratis", "Annuncio · Aiuto gratis", "Anúncio · Dica grátis"),
    ("You have {0} Dream Dust. Or watch a short ad for one free hint.", "Du hast {0} Traumstaub. Oder sieh dir eine kurze Werbung für einen Gratis-Tipp an.", "У вас {0} Пилу снів. Або перегляньте коротку рекламу й отримайте одну безкоштовну підказку.", "У вас {0} Пыли снов. Или посмотрите короткую рекламу и получите одну бесплатную подсказку.", "Tu as {0} Poussière de rêve. Ou regarde une courte pub pour un indice gratuit.", "Tienes {0} Polvo de sueños. O mira un anuncio corto para una pista gratis.", "Hai {0} Polvere dei sogni. Oppure guarda un breve annuncio per un aiuto gratis.", "Você tem {0} Pó dos sonhos. Ou veja um anúncio curto para ganhar uma dica grátis."),
    ("Free hint used.", "Gratis-Tipp verwendet.", "Безкоштовну підказку використано.", "Бесплатная подсказка использована.", "Indice gratuit utilisé.", "Pista gratis usada.", "Aiuto gratis usato.", "Dica grátis usada."),
    ("Free hint saved for your next hint.", "Gratis-Tipp für deinen nächsten Tipp gespeichert.", "Безкоштовну підказку збережено для наступного разу.", "Бесплатная подсказка сохранена на следующий раз.", "Indice gratuit gardé pour ton prochain indice.", "Pista gratis guardada para tu próxima pista.", "Aiuto gratis salvato per il prossimo aiuto.", "Dica grátis guardada para a próxima dica."),
    ("The board changed. Ask for a new hint.", "Das Spielfeld hat sich geändert. Frag nach einem neuen Tipp.", "Поле змінилося. Попросіть нову підказку.", "Поле изменилось. Попросите новую подсказку.", "Le plateau a changé. Demande un nouvel indice.", "El tablero cambió. Pide una pista nueva.", "Il tabellone è cambiato. Chiedi un nuovo aiuto.", "O tabuleiro mudou. Peça uma nova dica."),
    ("Privacy Choices", "Datenschutzoptionen", "Налаштування конфіденційності", "Настройки конфиденциальности", "Choix de confidentialité", "Opciones de privacidad", "Scelte sulla privacy", "Opções de privacidade"),
]

COSMETICS = [  # en, de, uk, ru, fr, es, it, pt-BR
    ("Classic", "Klassisch", "Класичний", "Классика", "Classique", "Clásico", "Classico", "Clássico"),
    ("Bouncy", "Hüpfend", "Стрибучий", "Прыгучий", "Rebondissant", "Saltarín", "Saltellante", "Saltitante"),
    ("Snoozy", "Schläfrig", "Сонько", "Соня", "Somnolent", "Dormilón", "Dormiglione", "Soneca"),
    ("Classic Jelly", "Klassik-Gelee", "Класичне желе", "Классическое желе", "Gelée classique", "Gelatina clásica", "Gelatina classica", "Geleia clássica"),
    ("Frosted Jelly", "Frost-Gelee", "Морозне желе", "Морозное желе", "Gelée givrée", "Gelatina escarchada", "Gelatina brinata", "Geleia gelada"),
    ("Milky Jelly", "Milch-Gelee", "Молочне желе", "Молочное желе", "Gelée lactée", "Gelatina de leche", "Gelatina al latte", "Geleia de leite"),
    ("Dewdrop Jelly", "Tautropfen-Gelee", "Желе-росинка", "Желе-росинка", "Gelée de rosée", "Gelatina de rocío", "Gelatina di rugiada", "Geleia de orvalho"),
    ("Velvet Jelly", "Samt-Gelee", "Оксамитове желе", "Бархатное желе", "Gelée velours", "Gelatina de terciopelo", "Gelatina vellutata", "Geleia de veludo"),
    ("Pearl Jelly", "Perlen-Gelee", "Перлинне желе", "Жемчужное желе", "Gelée nacrée", "Gelatina perlada", "Gelatina perlata", "Geleia perolada"),
    ("Toy Box", "Spielzeugkiste", "Іграшкова скриня", "Коробка игрушек", "Coffre à jouets", "Caja de juguetes", "Cesta dei giochi", "Baú de brinquedos"),
    ("Pastel", "Pastell", "Пастель", "Пастель", "Pastel", "Pastel", "Pastello", "Pastel"),
    ("Sorbet", "Sorbet", "Сорбет", "Сорбет", "Sorbet", "Sorbete", "Sorbetto", "Sorbet"),
    ("Sweet", "Süß", "Солодкий", "Сладкий", "Douceur", "Dulce", "Dolce", "Doce"),
    ("Button", "Knopf", "Ґудзик", "Пуговка", "Bouton", "Botón", "Bottone", "Botão"),
    ("Dozy", "Dösig", "Дрімота", "Дрёма", "Assoupi", "Adormilado", "Assonnato", "Sonolento"),
    ("Blush", "Rosig", "Рум'янець", "Румянец", "Rosé", "Rubor", "Rossore", "Rubor"),
    ("Starry", "Sternig", "Зоряний", "Звёздный", "Étoilé", "Estrellado", "Stellato", "Estrelado"),
    ("None", "Keins", "Немає", "Нет", "Aucun", "Ninguno", "Nessuno", "Nenhum"),
    ("Tiny Bow", "Kleine Schleife", "Бантик", "Бантик", "Mini ruban", "Lacito", "Fiocchetto", "Lacinho"),
    ("Little Flower", "Blümchen", "Квіточка", "Цветочек", "Petite fleur", "Florecita", "Fiorellino", "Florzinha"),
    ("Sprout Leaf", "Sprossblatt", "Паросток", "Росток", "Pousse", "Brote", "Germoglio", "Broto"),
    ("Ribbon", "Band", "Стрічка", "Ленточка", "Ruban", "Cinta", "Nastro", "Fita"),
    ("Little Star", "Sternchen", "Зірочка", "Звёздочка", "Petite étoile", "Estrellita", "Stellina", "Estrelinha"),
    ("Nightcap", "Schlafmütze", "Нічний ковпак", "Ночной колпак", "Bonnet de nuit", "Gorro de dormir", "Berretto da notte", "Touca de dormir"),
    ("Cloud Puff", "Wolkenbausch", "Хмаринка", "Облачко", "Nuage douillet", "Nubecita", "Nuvoletta", "Nuvenzinha"),
    ("Tiny Crown", "Krönchen", "Коронка", "Коронка", "Petite couronne", "Coronita", "Coroncina", "Coroinha"),
    ("Moon Pillow", "Mondkissen", "Місячна подушка", "Лунная подушка", "Oreiller lune", "Almohada lunar", "Cuscino lunare", "Travesseiro lunar"),
    ("Cloud Cushion", "Wolkenpolster", "Хмарна подушка", "Облачная подушка", "Coussin nuage", "Cojín de nube", "Cuscino di nuvola", "Almofada de nuvem"),
    ("Marshmallow", "Marshmallow", "Маршмелоу", "Маршмеллоу", "Guimauve", "Malvavisco", "Marshmallow", "Marshmallow"),
    ("Soft Flower", "Weiche Blume", "Ніжна квітка", "Нежный цветок", "Fleur douce", "Flor suave", "Fiore morbido", "Flor macia"),
    ("Crescent Bed", "Sichelbett", "Ліжко-півмісяць", "Кровать-месяц", "Lit croissant", "Cama creciente", "Letto a mezzaluna", "Cama crescente"),
    ("Dust Puff", "Staubwölkchen", "Пилова хмарка", "Пыльное облачко", "Nuage de poussière", "Nube de polvo", "Nuvola di polvere", "Nuvem de pó"),
    ("Sparkle", "Funkeln", "Іскри", "Искры", "Étincelles", "Destellos", "Scintille", "Brilhos"),
    ("Bubbles", "Blasen", "Бульбашки", "Пузырьки", "Bulles", "Burbujas", "Bolle", "Bolhas"),
    ("Star Trail", "Sternenspur", "Зоряний слід", "Звёздный след", "Traînée d'étoiles", "Estela de estrellas", "Scia di stelle", "Rastro de estrelas"),
    ("World Colors", "Weltfarben", "Кольори світу", "Цвета мира", "Couleurs du monde", "Colores del mundo", "Colori del mondo", "Cores do mundo"),
    ("Honey Tray", "Honigtablett", "Медова таця", "Медовый поднос", "Plateau miel", "Bandeja de miel", "Vassoio di miele", "Bandeja de mel"),
    ("Mint Garden", "Minzgarten", "М'ятний сад", "Мятный сад", "Jardin menthe", "Jardín de menta", "Giardino di menta", "Jardim de menta"),
    ("Peach Quilt", "Pfirsichdecke", "Персикова ковдра", "Персиковое одеяло", "Édredon pêche", "Edredón melocotón", "Trapunta pesca", "Colcha de pêssego"),
    ("Lavender Dream", "Lavendeltraum", "Лавандовий сон", "Лавандовый сон", "Rêve lavande", "Sueño de lavanda", "Sogno di lavanda", "Sonho de lavanda"),
    ("Starlight", "Sternenlicht", "Зоряне світло", "Звёздный свет", "Lumière d'étoiles", "Luz de estrellas", "Luce di stelle", "Luz das estrelas"),
]


def main():
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Localization", "Resources", "Localization")
    os.makedirs(root, exist_ok=True)
    rows = ROWS + COSMETICS
    keys, seen = [], set()
    for r in rows:
        key = r[0][2:] if r[0].startswith("P:") else r[0]
        assert key not in seen, "duplicate " + key
        seen.add(key)
        plural = r[0].startswith("P:")
        n = 9 if plural else 8
        assert len(r) == n, (key, len(r))
        for i, lang in enumerate(LANGS):
            t = r[1 + i]
            forms = t.split("|")
            if plural:
                assert len(forms) == (3 if lang in ("uk", "ru") else 2), (lang, key)
            else:
                assert len(forms) == 1, (lang, key)
            assert "œ" not in t and "Œ" not in t, "Nunito has no œ: " + t
            assert t.count("{") == key.count("{") or plural, (lang, key, t)
        keys.append((key, r[-1] if plural else None))
    with open(os.path.join(root, "source.txt"), "w", encoding="utf-8") as f:
        f.write("# PLOP! localization source: English keys (plural keys: English one-form after a tab).\n")
        for k, one in keys:
            f.write(k + ("\t" + one if one else "") + "\n")
    for i, lang in enumerate(LANGS):
        with open(os.path.join(root, lang + ".txt"), "w", encoding="utf-8") as f:
            f.write("# PLOP! — %s (%s)\n" % (NAMES[lang], lang))
            f.write("# STATUS: drafted by an AI model (Claude) for Final Sprint 02. NOT reviewed by a native speaker.\n")
            f.write("# Format: English source<TAB>translation. Plural forms separated by | (one|other; uk/ru: one|few|many).\n")
            for r in rows:
                key = r[0][2:] if r[0].startswith("P:") else r[0]
                f.write(key + "\t" + r[1 + i] + "\n")
    print("%d keys -> %s" % (len(keys), os.path.normpath(root)))


if __name__ == "__main__":
    main()
