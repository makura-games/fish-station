centcomm-supply-window-title = CentComm Supply
centcomm-supply-window-account-name-label = Account name:
centcomm-supply-window-account-name = [bold]Central Command[/bold] [font="Monospace"]\[CC\][/font]
centcomm-supply-window-station = Station:
centcomm-supply-window-search-placeholder = Search
centcomm-supply-window-categories-label = Categories:
centcomm-supply-window-categories-all = All
centcomm-supply-window-order = Order
centcomm-supply-window-product-quantity = { $count }/{ $max }
centcomm-supply-window-order-button = Send
centcomm-supply-window-no-station = No station selected.
centcomm-supply-window-empty-order = Select at least one product.
centcomm-supply-window-flavor-left = Central Command Supply Division
centcomm-supply-window-flavor-right = v2.0

centcomm-supply-order-window-title = Add to order
centcomm-supply-order-window-product = Product
centcomm-supply-order-window-description = Description
centcomm-supply-order-window-amount = Amount
centcomm-supply-order-window-add = Add
centcomm-supply-order-row-title = { $product } × { $count }
centcomm-supply-order-row-remove = Remove

centcomm-supply-unknown-station = Unknown station
centcomm-supply-announcement = Attention! Central Command has approved special equipment order #{ $number } for { $station }. The cargo will be delivered to the supply department shortly.
centcomm-supply-announcement-sender = Nanotrasen
centcomm-supply-order-accepted = Order #{ $number } accepted. Delivery to the supply department in { $seconds } sec.
centcomm-supply-paper-name = Supply order #{ $orderNumber }
centcomm-supply-paper-text =
    [head=2]Supply order #{ $orderNumber }[/head]
    { "[bold]Station:[/bold]" } { $station }
    { "[bold]Requester:[/bold]" } Central Command
    { "[bold]Sender:[/bold]" } Nanotrasen
    { "[bold]Reason:[/bold]" } special equipment
    { "[bold]Approved by:[/bold]" } Nanotrasen
    { "[head=3]Order contents[/head]" }
    { $contents }

centcomm-supply-error-access = Access denied.
centcomm-supply-error-invalid-station = Invalid station.
centcomm-supply-error-cooldown = Please wait { $seconds } second(s) before ordering again.
centcomm-supply-error-empty = The order is empty.
centcomm-supply-error-too-many-products = Too many products in one order (max { $max }).
centcomm-supply-error-duplicate = Duplicate product in order.
centcomm-supply-error-unknown-product = Unknown product.
centcomm-supply-error-invalid-quantity = Invalid quantity for { $product }.
centcomm-supply-error-invalid-product = Product is misconfigured.
centcomm-supply-error-bad-config = Crate configuration is invalid.
centcomm-supply-error-too-many-crates = The order requires too many crates (max { $max }).
centcomm-supply-error-no-pallets = Not enough free cargo pallets: need { $required }, available { $available }.
centcomm-supply-error-pack-failed = Failed to pack the order.

centcomm-supply-category-general = General
centcomm-supply-category-medical = Medical
centcomm-supply-category-weapons = Weapons
centcomm-supply-category-ammo = Ammunition
centcomm-supply-category-blueprints = Blueprints

centcomm-supply-product-medical-bundle-name = Nanotrasen medical bundle
centcomm-supply-product-medical-bundle-desc = A Nanotrasen duffel bag filled with a compact defibrillator, combat medkits and combat medipens.
centcomm-supply-product-ash12-duffel-name = Blue Shield ASH-12 duffel
centcomm-supply-product-ash12-duffel-desc = A Blue Shield duffel containing an ASH-12 rifle, a slug magazine and a beanbag magazine.
centcomm-supply-product-ash12-slug-name = ASH-12 slug magazine
centcomm-supply-product-ash12-slug-desc = A magazine loaded with slug rounds for the ASH-12.
centcomm-supply-product-ash12-blueprint-name = ASH-12 magazine blueprint
centcomm-supply-product-ash12-blueprint-desc = A blueprint with a schematic of an ASH-12 magazine with all basic ammo variants.
