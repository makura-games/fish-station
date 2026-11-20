using Content.Client._Fish.Graphics.Bloom;
using Content.Client.Graphics;
using Content.Client.Interactable.Components;
using Content.Client.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Fish.Graphics;

[TestFixture]
public sealed class FishBloomVisibilityTest
{
    /// <summary>
    /// Sources and blockers follow sprite visibility, preserve normal outlines and exclude cloaked sprites
    /// or unsupported postshaders.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task SpriteEligibilityPreservesStealthAndInteractionOutline(bool emissive)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        EntityUid uid = default;
        SpriteComponent sprite = null;
        InteractionOutlineComponent outline = null;
        ShaderInstance otherShader = null;
        ShaderInstance outlineShader = null;
        FishBloomOverlay bloom = null;

        try
        {
            await client.WaitPost(() =>
            {
                uid = client.EntMan.SpawnEntity("Pen", MapCoordinates.Nullspace);
                sprite = client.EntMan.GetComponent<SpriteComponent>(uid);
                var spriteSystem = client.System<SpriteSystem>();
                if (emissive)
                    spriteSystem.LayerSetData((uid, sprite), 0, new PrototypeLayerData
                    {
                        Shader = SpriteSystem.UnshadedId,
                    });

                bloom = new FishBloomOverlay(client.EntMan);
                outline = client.EntMan.EnsureComponent<InteractionOutlineComponent>(uid);
                outline.Active = true;
                ProtoId<ShaderPrototype> outlineProto = "SelectionOutlineInrange";
                outlineShader = client.ProtoMan.Index(outlineProto).InstanceUnique();
                spriteSystem.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.InteractionOutline, outlineShader));
                // ID проверяется клиентом; серверный YAML-линтер не регистрирует ShaderPrototype.
                ProtoId<ShaderPrototype> stealthShader = "Stealth";
                otherShader = client.ProtoMan.Index(stealthShader).InstanceUnique();
            });
            await client.WaitAssertion(() =>
            {
                Assert.That(outlineShader, Is.Not.Null);
                Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.True,
                    "A normal interaction outline must not remove bloom sources or blockers.");
            });

            await client.WaitPost(() => sprite.PostShader = otherShader);
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.False,
                "An outline component alone does not make an unrelated postshader safe to bypass."));
            await client.WaitPost(() =>
            {
                outline.Active = false;
                client.System<SpriteSystem>().RemovePostShader((uid, sprite), ContentPostShaderIds.InteractionOutline);
                sprite.PostShader = null;
                client.EntMan.EnsureComponent<StealthComponent>(uid);
            });
            await pair.RunTicksSync(1);
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.False));

            // Скрытый предмет нельзя раскрыть даже при временной подмене его постшейдера другой системой.
            await client.WaitPost(() => sprite.PostShader = null);
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.False));

            await client.WaitPost(() => client.System<StealthSystem>().SetEnabled(uid, false));
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.True));

            await client.WaitPost(() => client.System<SpriteSystem>().SetVisible((uid, sprite), false));
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.False));
            await client.WaitPost(() =>
            {
                client.System<SpriteSystem>().SetVisible((uid, sprite), true);
                client.System<SpriteSystem>().SetColor((uid, sprite), Color.Transparent);
            });
            await client.WaitAssertion(() => Assert.That(bloom.CanDrawSprite((uid, sprite)), Is.False));
        }
        finally
        {
            await client.WaitPost(() =>
            {
                if (client.EntMan.EntityExists(uid))
                {
                    if (outline != null)
                        outline.Active = false;
                    sprite.PostShader = null;
                    client.EntMan.DeleteEntity(uid);
                }

                outlineShader?.Dispose();
                otherShader?.Dispose();
                bloom?.Dispose();
            });
        }

        await pair.CleanReturnAsync();
    }
}
