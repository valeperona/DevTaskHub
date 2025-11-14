/// <reference types="cypress" />

const PROJECT_ID = '58997d35-b7f1-4a67-bb1c-8d67025afa00';

const login = () => {
  cy.visit('/login');
  cy.get('[data-cy="login-email-input"]').type('valeperona@gmail.com');
  cy.get('[data-cy="login-submit-button"]').click();
  cy.visit(`/projects/${PROJECT_ID}`);
};

const findTaskCard = (title: string) =>
  cy.contains('[data-cy="task-card"] h4', title).parents('[data-cy="task-card"]');

describe('Crear tarea', () => {
  beforeEach(() => {
    login();
  });

  it('debería crear una nueva tarea exitosamente', () => {
    const taskTitle = `Tarea creada desde Cypress ${Date.now()}`;
    cy.get('[data-cy="task-title-input"]').type(taskTitle);
    cy.get('[data-cy="task-desc-input"]').type('Descripción de prueba E2E');
    cy.get('[data-cy="task-priority-select"]').select('Medium');
    cy.get('[data-cy="task-save-button"]').click();
    cy.contains(taskTitle).should('exist');
  });

  it('permite actualizar estado y prioridad de una tarea existente', () => {
    const taskTitle = `Tarea para actualizar ${Date.now()}`;
    cy.get('[data-cy="task-title-input"]').type(taskTitle);
    cy.get('[data-cy="task-desc-input"]').type('Tarea creada para actualizar');
    cy.get('[data-cy="task-priority-select"]').select('Low');
    cy.get('[data-cy="task-save-button"]').click();

    findTaskCard(taskTitle).within(() => {
      cy.get('[data-cy="task-status-select"]').select('InProgress');
      cy.get('[data-cy="task-priority-select-existing"]').select('High');
    });

    cy.get(`[data-cy="column-InProgress"]`)
      .contains('[data-cy="task-card"] h4', taskTitle)
      .parents('[data-cy="task-card"]')
      .within(() => {
        cy.get('[data-cy="task-priority-select-existing"]').should('have.value', 'High');
      });
  });

  it('muestra un error si se intenta crear una tarea sin título', () => {
    cy.get('[data-cy="task-title-input"]').clear();
    cy.get('[data-cy="task-save-button"]').click();
    cy.get('[data-cy="error-message"]').should('contain.text', 'El título de la tarea es obligatorio');
  });
});
