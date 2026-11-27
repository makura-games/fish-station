id-card-reader-slot-name = ID card

signal-port-name-fish-id-card-inserted = Card inserted
signal-port-description-fish-id-card-inserted = Invoked whenever an ID card is inserted into the reader.

signal-port-name-fish-id-card-removed = Card removed
signal-port-description-fish-id-card-removed = Invoked whenever an ID card is removed from the reader.

signal-port-name-fish-id-card-access-granted = Access granted
signal-port-description-fish-id-card-access-granted = High while the configured number of readers are authorized; low otherwise.

signal-port-name-fish-id-card-local-authorization = Card authorized
signal-port-description-fish-id-card-local-authorization = High while this reader's inserted ID card has the required access.

signal-port-name-fish-id-card-reader-close-request = Close request
signal-port-description-fish-id-card-reader-close-request = Invoked after the opening delay and repeated while a linked door reports that it is open.

signal-port-name-fish-id-card-reader-authorization = Reader authorization
signal-port-description-fish-id-card-reader-authorization = Accepts the persistent access-granted state from another ID card reader.

signal-port-name-fish-id-card-reader-door-status = Door status
signal-port-description-fish-id-card-reader-door-status = Accepts a persistent door-status signal so failed closing attempts can be repeated.
