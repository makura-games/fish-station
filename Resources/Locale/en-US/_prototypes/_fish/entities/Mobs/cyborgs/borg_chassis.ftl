## Borg Mk2: имена наследуются от обычных chassis с суффиксом Mk2,
## описание — как у селективного chassis (механически они идентичны).
ent-FishBorgChassisGenericMk2 = { ent-BorgChassisGeneric } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisEngineerMk2 = { ent-BorgChassisEngineer } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisMiningMk2 = { ent-BorgChassisMining } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisJanitorMk2 = { ent-BorgChassisJanitor } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisMedicalMk2 = { ent-BorgChassisMedical } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisServiceMk2 = { ent-BorgChassisService } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisPeaceMk2 = { ent-BorgChassisPeace } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisClownMk2 = { ent-BorgChassisClown } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

ent-FishBorgChassisSecurityMk2 = { ent-BorgChassisSecurity } Mk2
    .desc = { ent-BorgChassisSelectable.desc }

## Battery-варианты: борг приходит с мозгом и полной ячейкой, имя наследуется от Mk2.
ent-FishBorgChassisMedicalMk2Battery = { ent-FishBorgChassisMedicalMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisMedicalMk2.desc }

ent-FishBorgChassisGenericMk2Battery = { ent-FishBorgChassisGenericMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisGenericMk2.desc }

ent-FishBorgChassisEngineerMk2Battery = { ent-FishBorgChassisEngineerMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisEngineerMk2.desc }

ent-FishBorgChassisMiningMk2Battery = { ent-FishBorgChassisMiningMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisMiningMk2.desc }

ent-FishBorgChassisJanitorMk2Battery = { ent-FishBorgChassisJanitorMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisJanitorMk2.desc }

ent-FishBorgChassisServiceMk2Battery = { ent-FishBorgChassisServiceMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisServiceMk2.desc }

ent-FishBorgChassisPeaceMk2Battery = { ent-FishBorgChassisPeaceMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisPeaceMk2.desc }

ent-FishBorgChassisClownMk2Battery = { ent-FishBorgChassisClownMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisClownMk2.desc }

ent-FishBorgChassisSecurityMk2Battery = { ent-FishBorgChassisSecurityMk2 }
    .suffix = Battery
    .desc = { ent-FishBorgChassisSecurityMk2.desc }

## Синди-риперы: Fish-вариант с постоянными chassis-руками; имя и описание
## наследуются от Sunrise-шасси. Derelict без .desc — берётся из YAML-родителя.
ent-FishBorgChassisSyndicateReaper = { ent-BorgChassisSyndicateReaper }
    .desc = { ent-BorgChassisSyndicateReaper.desc }
ent-FishBorgChassisSyndicateReaperDerelict = { ent-SyndicateReaperBorgChassisDerelictSunrise }
