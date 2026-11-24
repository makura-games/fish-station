fish-cyberpsychosis-wand-applied = Киберпсихоз: { $level }%
fish-cyberpsychosis-examine-low = [color=#6E6E6E]{ CAPITALIZE(SUBJECT($target)) } { GENDER($target) ->
        [male] кажется слегка рассеянным
        [female] кажется слегка рассеянной
        [epicene] кажутся слегка рассеянными
       *[neuter] кажется слегка рассеянным
    }, глаза время от времени подёргиваются.[/color]
fish-cyberpsychosis-examine-medium = [color=#CD5C5C]{ CAPITALIZE(SUBJECT($target)) } выглядит нестабильно, заметны нервный тик, расширенные зрачки и напряжённая походка.[/color]
fish-cyberpsychosis-examine-high = [color=#B22222]{ CAPITALIZE(SUBJECT($target)) } в состоянии острого киберпсихоза, взгляд расфокусирован, движения дёрганые и пугающе неестественные.[/color]
fish-cyberpsychosis-resistance-examine = - Накопление [color=#8B0000]киберпсихоза[/color] снижается на [color=lightblue]{ $percent }%[/color].

ent-FishDebugWandCyberpsychosisAdd10 = дебаг-палка киберпсихоза (+10%)
    .desc = Экспериментальная отладочная палка, добавляющая 10% киберпсихоза.
    .suffix = Fish

ent-FishDebugWandCyberpsychosisAdd30 = дебаг-палка киберпсихоза (+30%)
    .desc = Экспериментальная отладочная палка, добавляющая 30% киберпсихоза.
    .suffix = Fish

ent-FishDebugWandCyberpsychosisRemove10 = дебаг-палка анти-киберпсихоза (-10%)
    .desc = Экспериментальная отладочная палка, снимающая 10% киберпсихоза.
    .suffix = Fish

ent-FishDebugWandCyberpsychosisRemove30 = дебаг-палка анти-киберпсихоза (-30%)
    .desc = Экспериментальная отладочная палка, снимающая 30% киберпсихоза.
    .suffix = Fish
